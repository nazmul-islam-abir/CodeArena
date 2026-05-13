using MyMvcApp.Data;
using MyMvcApp.Models;
using Newtonsoft.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MyMvcApp.Services
{
    public class JudgeService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<JudgeService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _judge0ApiUrl;

        public JudgeService(IServiceScopeFactory scopeFactory, IHttpClientFactory httpClientFactory, ILogger<JudgeService> logger, IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _configuration = configuration;

            _judge0ApiUrl = _configuration["Judge0:ApiUrl"] ?? "http://localhost:2358/";
            if (!_judge0ApiUrl.EndsWith("/"))
            {
                _judge0ApiUrl += "/";
            }

            _logger.LogInformation($"JudgeService initialized with Judge0 API URL: {_judge0ApiUrl}");
        }

        public async Task<int> SubmitAsync(int userId, int problemId, string sourceCode, int languageId, string languageName, int? contestId = null)
        {
            // Create submission record with "Processing" status
            var submission = new Submission
            {
                UserId = userId,
                ProblemId = problemId,
                LanguageId = languageId,
                LanguageName = languageName,
                SourceCode = sourceCode,
                Verdict = "Processing",
                SubmittedAt = DateTime.UtcNow,
                ContestId = contestId,
                UniqueId = Guid.NewGuid().ToString("N")
            };

            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Get user and problem info
                var user = await dbContext.Users.FindAsync(userId);
                if (user != null) submission.UserName = user.Username;

                var problem = await dbContext.Problems.FindAsync(problemId);
                if (problem != null) submission.ProblemTitle = problem.Title;

                // Save submission to database
                dbContext.Submissions.Add(submission);
                await dbContext.SaveChangesAsync();
                var submissionId = submission.Id;

                _logger.LogInformation($"Submission {submissionId} created, starting background processing");

                // Start background processing
                _ = Task.Run(() => ProcessSubmissionAsync(submissionId, sourceCode, languageId, problemId, contestId));

                return submissionId;
            }
        }

        private async Task ProcessSubmissionAsync(int submissionId, string sourceCode, int languageId, int problemId, int? contestId)
        {
            _logger.LogInformation($"Processing submission {submissionId} started");

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            try
            {
                var submission = await dbContext.Submissions.FindAsync(submissionId);
                if (submission == null)
                {
                    _logger.LogError($"Submission {submissionId} not found");
                    return;
                }

                // Get problem details
                var problem = await dbContext.Problems.FindAsync(problemId);
                if (problem == null)
                {
                    submission.Verdict = "CE";
                    submission.ErrorMessage = "Problem not found";
                    await dbContext.SaveChangesAsync();
                    _logger.LogError($"Problem {problemId} not found for submission {submissionId}");
                    return;
                }

                // Get test cases
                var testCases = await dbContext.TestCases
                    .Where(tc => tc.ProblemId == problemId)
                    .OrderBy(tc => tc.Order)
                    .ToListAsync();

                if (testCases == null || testCases.Count == 0)
                {
                    submission.Verdict = "CE";
                    submission.ErrorMessage = "No test cases found";
                    await dbContext.SaveChangesAsync();
                    _logger.LogError($"No test cases for problem {problemId}");
                    return;
                }

                _logger.LogInformation($"Found {testCases.Count} test cases for problem {problemId}");

                int passedTests = 0;
                double maxTime = 0;
                int maxMemory = 0;

                // Run each test case
                for (int i = 0; i < testCases.Count; i++)
                {
                    var testCase = testCases[i];
                    _logger.LogInformation($"Running test case {i + 1}/{testCases.Count} for submission {submissionId}");

                    var testResult = await RunSingleTestCaseAsync(sourceCode, languageId, testCase, problem.TimeLimit, problem.MemoryLimit);

                    if (testResult.IsSuccess)
                    {
                        var actualOutput = testResult.Output?.Trim() ?? string.Empty;
                        var expectedOutput = testCase.ExpectedOutput?.Trim() ?? string.Empty;

                        if (actualOutput == expectedOutput)
                        {
                            passedTests++;
                            _logger.LogInformation($"Test case {i + 1} passed");
                        }
                        else
                        {
                            submission.Verdict = "WA";
                            submission.ExecutionTime = testResult.ExecutionTime;
                            submission.MemoryUsed = testResult.MemoryUsed;
                            submission.TestCasesPassed = passedTests;
                            submission.TotalTestCases = testCases.Count;

                            if (testCase.IsSample)
                            {
                                submission.ErrorMessage = $"Failed on sample test case. Expected: {expectedOutput}, Got: {actualOutput}";
                            }

                            await dbContext.SaveChangesAsync();
                            _logger.LogWarning($"Submission {submissionId} failed: WA on test case {i + 1}");
                            return;
                        }
                    }
                    else
                    {
                        submission.Verdict = testResult.Verdict;
                        submission.ErrorMessage = testResult.ErrorMessage;
                        submission.ExecutionTime = testResult.ExecutionTime;
                        submission.MemoryUsed = testResult.MemoryUsed;
                        submission.TestCasesPassed = passedTests;
                        submission.TotalTestCases = testCases.Count;

                        await dbContext.SaveChangesAsync();
                        _logger.LogWarning($"Submission {submissionId} failed: {testResult.Verdict} on test case {i + 1} - {testResult.ErrorMessage}");
                        return;
                    }

                    if (testResult.ExecutionTime > maxTime) maxTime = testResult.ExecutionTime;
                    if (testResult.MemoryUsed > maxMemory) maxMemory = testResult.MemoryUsed;
                }

                // All tests passed
                submission.Verdict = "AC";
                submission.ExecutionTime = maxTime;
                submission.MemoryUsed = maxMemory;
                submission.TestCasesPassed = passedTests;
                submission.TotalTestCases = testCases.Count;

                await dbContext.SaveChangesAsync();

                _logger.LogInformation($"Submission {submissionId} completed: AC! {passedTests}/{testCases.Count} tests passed");

                // Update statistics
                await UpdateUserStatisticsAsync(dbContext, submission.UserId, problemId, problem.Points);
                await UpdateProblemStatisticsAsync(dbContext, problemId, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing submission {submissionId}");

                var submission = await dbContext.Submissions.FindAsync(submissionId);
                if (submission != null)
                {
                    submission.Verdict = "CE";
                    submission.ErrorMessage = ex.Message;
                    await dbContext.SaveChangesAsync();
                }
            }
        }

        public async Task RejudgeSubmissionAsync(int submissionId)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var submission = await dbContext.Submissions.FindAsync(submissionId);
            if (submission == null) return;

            await ProcessSubmissionAsync(
                submissionId,
                submission.SourceCode,
                submission.LanguageId,
                submission.ProblemId,
                submission.ContestId
            );
        }

        private async Task<TestCaseResult> RunSingleTestCaseAsync(string sourceCode, int languageId, TestCase testCase, int timeLimitSeconds, int memoryLimitKb)
        {
            var result = new TestCaseResult();

            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(60); // Increased timeout
            client.BaseAddress = new Uri(_judge0ApiUrl);

            try
            {
                _logger.LogInformation($"Calling Judge0 API at {_judge0ApiUrl}");

                var requestBody = new
                {
                    language_id = languageId,
                    source_code = sourceCode,
                    stdin = testCase.Input ?? string.Empty,
                    expected_output = testCase.ExpectedOutput ?? string.Empty,
                    cpu_time_limit = timeLimitSeconds <= 0 ? 2 : timeLimitSeconds,
                    memory_limit = memoryLimitKb <= 0 ? 256000 : memoryLimitKb
                };

                var content = new StringContent(
                    JsonConvert.SerializeObject(requestBody),
                    Encoding.UTF8,
                    "application/json"
                );

                var submitResponse = await client.PostAsync(
                    "submissions?base64_encoded=false&wait=false",
                    content
                );

                if (!submitResponse.IsSuccessStatusCode)
                {
                    var errorBody = await submitResponse.Content.ReadAsStringAsync();
                    _logger.LogError($"Judge0 API error: {submitResponse.StatusCode}, Body: {errorBody}");
                    result.IsSuccess = false;
                    result.Verdict = "CE";
                    result.ErrorMessage = $"API Error: {submitResponse.StatusCode}";
                    return result;
                }

                var submitResult = await submitResponse.Content.ReadAsStringAsync();
                var submitJson = JsonConvert.DeserializeObject<dynamic>(submitResult);

                string token = submitJson?.token;
                if (string.IsNullOrEmpty(token))
                {
                    result.IsSuccess = false;
                    result.Verdict = "CE";
                    result.ErrorMessage = "Failed to get submission token";
                    return result;
                }

                _logger.LogInformation($"Got token: {token}");

                int maxAttempts = 30;
                int attempt = 0;
                dynamic? finalJson = null;

                while (attempt < maxAttempts)
                {
                    await Task.Delay(2000);

                    var getResponse = await client.GetAsync(
                        $"submissions/{token}?base64_encoded=false"
                    );

                    if (getResponse.IsSuccessStatusCode)
                    {
                        var finalResult = await getResponse.Content.ReadAsStringAsync();
                        finalJson = JsonConvert.DeserializeObject<dynamic>(finalResult);

                        if (finalJson?.status?.id != null)
                        {
                            int currentStatusId = (int)finalJson.status.id;
                            if (currentStatusId >= 3)
                            {
                                break;
                            }
                        }
                    }
                    attempt++;
                }

                if (finalJson == null)
                {
                    result.IsSuccess = false;
                    result.Verdict = "CE";
                    result.ErrorMessage = "No response from judge server";
                    return result;
                }

                int statusId = (int)(finalJson.status?.id ?? 0);
                _logger.LogInformation($"Final status: {statusId} - {finalJson.status?.description}");

                if (statusId == 3)
                {
                    result.IsSuccess = true;
                    result.Output = finalJson.stdout?.ToString() ?? string.Empty;
                    result.ExecutionTime = finalJson.time != null ? (double)finalJson.time : 0;
                    result.MemoryUsed = finalJson.memory != null ? (int)finalJson.memory : 0;
                    return result;
                }
                else if (statusId == 4)
                {
                    result.IsSuccess = false;
                    result.Verdict = "WA";
                    result.Output = finalJson.stdout?.ToString() ?? string.Empty;
                    result.ExecutionTime = finalJson.time != null ? (double)finalJson.time : 0;
                    result.MemoryUsed = finalJson.memory != null ? (int)finalJson.memory : 0;
                    return result;
                }
                else if (statusId == 5)
                {
                    result.IsSuccess = false;
                    result.Verdict = "TLE";
                    return result;
                }
                else if (statusId == 6)
                {
                    result.IsSuccess = false;
                    result.Verdict = "MLE";
                    return result;
                }
                else if (statusId == 7)
                {
                    result.IsSuccess = false;
                    result.Verdict = "RE";
                    result.ErrorMessage = finalJson.stderr?.ToString() ?? "Runtime Error";
                    return result;
                }
                else if (statusId == 8)
                {
                    result.IsSuccess = false;
                    result.Verdict = "CE";
                    result.ErrorMessage = finalJson.compile_output?.ToString() ?? "Compilation Error";
                    return result;
                }
                else
                {
                    result.IsSuccess = false;
                    result.Verdict = "CE";
                    result.ErrorMessage = $"Unknown status: {statusId}";
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception in RunSingleTestCaseAsync: {ex.Message}");
                result.IsSuccess = false;
                result.Verdict = "CE";
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        private async Task UpdateUserStatisticsAsync(AppDbContext dbContext, int userId, int problemId, int points)
        {
            try
            {
                var solvedCount = await dbContext.Submissions
                    .CountAsync(s => s.UserId == userId && s.ProblemId == problemId && s.Verdict == "AC");

                if (solvedCount == 1)
                {
                    var user = await dbContext.Users.FindAsync(userId);
                    if (user != null)
                    {
                        user.ProblemsSolved += 1;
                        user.TotalPoints += points;
                        await dbContext.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user statistics");
            }
        }

        private async Task UpdateProblemStatisticsAsync(AppDbContext dbContext, int problemId, bool isSolved)
        {
            try
            {
                var problem = await dbContext.Problems.FindAsync(problemId);
                if (problem != null)
                {
                    problem.SubmissionCount++;
                    if (isSolved)
                    {
                        problem.SolvedCount++;
                    }
                    await dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating problem statistics");
            }
        }
    }

    public class TestCaseResult
    {
        public bool IsSuccess { get; set; }
        public string Output { get; set; } = string.Empty;
        public double ExecutionTime { get; set; }
        public int MemoryUsed { get; set; }
        public string Verdict { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }
}