// Services/CodeforcesService.cs
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using MyMvcApp.Models;
using Npgsql;
using HtmlAgilityPack; // Add this

namespace MyMvcApp.Services
{
    public class CodeforcesService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _apiSecret;
        private readonly string _connectionString;
        private readonly ILogger<CodeforcesService> _logger;

        public CodeforcesService(IHttpClientFactory httpClientFactory, 
                                  IConfiguration configuration,
                                  ILogger<CodeforcesService> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            _httpClient.BaseAddress = new Uri("https://codeforces.com/api/");
            _apiKey = configuration["Codeforces:ApiKey"];
            _apiSecret = configuration["Codeforces:ApiSecret"];
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _logger = logger;
        }

        // Generate API signature for authenticated requests
        private string GenerateApiSignature(string methodName, Dictionary<string, string> parameters)
        {
            long time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            parameters["apiKey"] = _apiKey;
            parameters["time"] = time.ToString();

            // Sort parameters alphabetically
            var sortedParams = parameters.OrderBy(p => p.Key)
                                         .Select(p => $"{p.Key}={p.Value}");
            var paramString = string.Join("&", sortedParams);

            // Create random 6-digit number
            Random rand = new Random();
            string randNum = rand.Next(100000, 999999).ToString();

            // Create signature string: rand + / + methodName + ? + paramString + # + apiSecret
            string signatureString = $"{randNum}/{methodName}?{paramString}#{_apiSecret}";

            // Calculate SHA-512 hash
            using (SHA512 sha512 = SHA512.Create())
            {
                byte[] hashBytes = sha512.ComputeHash(Encoding.UTF8.GetBytes(signatureString));
                string hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                return $"{randNum}{hash}";
            }
        }

        // Fetch all problems from Codeforces
        public async Task<List<CodeforcesProblem>> FetchAllProblems(int limit = 1000)
        {
            var problems = new List<CodeforcesProblem>();
            
            try
            {
                _logger.LogInformation("Fetching problems from Codeforces API...");
                var response = await _httpClient.GetAsync("problemset.problems");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var data = JObject.Parse(json);
                    
                    if (data["status"].ToString() == "OK")
                    {
                        var problemsArray = data["result"]["problems"] as JArray;
                        var statisticsArray = data["result"]["problemStatistics"] as JArray;
                        
                        _logger.LogInformation($"Found {problemsArray.Count} problems");
                        
                        // Create a dictionary for quick lookup of statistics
                        var statsDict = new Dictionary<string, JToken>();
                        foreach (var stat in statisticsArray)
                        {
                            string key = $"{stat["contestId"]}-{stat["index"]}";
                            statsDict[key] = stat;
                        }
                        
                        int count = 0;
                        foreach (var problem in problemsArray)
                        {
                            if (count >= limit) break;
                            
                            string key = $"{problem["contestId"]}-{problem["index"]}";
                            var stats = statsDict.ContainsKey(key) ? statsDict[key] : null;
                            
                            var cfProblem = new CodeforcesProblem
                            {
                                ContestId = problem["contestId"]?.Value<int>(),
                                Index = problem["index"]?.Value<string>(),
                                Name = problem["name"]?.Value<string>(),
                                Type = problem["type"]?.Value<string>(),
                                Points = problem["points"]?.Value<double?>(),
                                Rating = problem["rating"]?.Value<int?>(),
                                Tags = problem["tags"]?.Select(t => t.Value<string>()).ToList(),
                                SolvedCount = stats?["solvedCount"]?.Value<int?>() ?? 0
                            };
                            
                            problems.Add(cfProblem);
                            count++;
                            
                            // Save to database immediately
                            await SaveProblemToDatabase(cfProblem);
                        }
                        
                        _logger.LogInformation($"Successfully imported {problems.Count} problems");
                    }
                    else
                    {
                        _logger.LogError($"API Error: {data["comment"]}");
                    }
                }
                else
                {
                    _logger.LogError($"HTTP Error: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Codeforces problems");
            }
            
            return problems;
        }

        // Fetch problems by rating range
        public async Task<List<CodeforcesProblem>> FetchProblemsByRating(int minRating, int maxRating, int limit = 100)
        {
            var allProblems = await FetchAllProblems(1000);
            return allProblems.Where(p => p.Rating >= minRating && p.Rating <= maxRating)
                             .Take(limit)
                             .ToList();
        }

        // Fetch problems by tags
        public async Task<List<CodeforcesProblem>> FetchProblemsByTags(List<string> tags, int limit = 100)
        {
            var allProblems = await FetchAllProblems(1000);
            return allProblems.Where(p => p.Tags != null && tags.Any(t => p.Tags.Contains(t)))
                             .Take(limit)
                             .ToList();
        }

        // Fetch contest problems
        public async Task<List<CodeforcesProblem>> FetchContestProblems(int contestId)
        {
            var problems = new List<CodeforcesProblem>();
            
            try
            {
                _logger.LogInformation($"Fetching contest {contestId} from Codeforces API...");
                var response = await _httpClient.GetAsync($"contest.standings?contestId={contestId}&from=1&count=1");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var data = JObject.Parse(json);
                    
                    if (data["status"].ToString() == "OK")
                    {
                        var problemsArray = data["result"]["problems"] as JArray;
                        
                        foreach (var problem in problemsArray)
                        {
                            var cfProblem = new CodeforcesProblem
                            {
                                ContestId = problem["contestId"]?.Value<int>(),
                                Index = problem["index"]?.Value<string>(),
                                Name = problem["name"]?.Value<string>(),
                                Type = problem["type"]?.Value<string>(),
                                Points = problem["points"]?.Value<double?>(),
                                Rating = problem["rating"]?.Value<int?>(),
                                Tags = problem["tags"]?.Select(t => t.Value<string>()).ToList()
                            };
                            
                            problems.Add(cfProblem);
                            
                            // Fetch full problem statement
                            var fullProblem = await FetchFullProblemStatement(cfProblem.ContestId.Value, cfProblem.Index);
                            if (fullProblem != null)
                            {
                                cfProblem.Description = fullProblem.Description;
                                cfProblem.InputFormat = fullProblem.InputFormat;
                                cfProblem.OutputFormat = fullProblem.OutputFormat;
                                cfProblem.SampleInput = fullProblem.SampleInput;
                                cfProblem.SampleOutput = fullProblem.SampleOutput;
                                cfProblem.Constraints = fullProblem.Constraints;
                            }
                            
                            await SaveProblemToDatabase(cfProblem);
                        }
                        
                        _logger.LogInformation($"Found {problems.Count} problems in contest {contestId}");
                    }
                    else
                    {
                        _logger.LogError($"API Error for contest {contestId}: {data["comment"]}");
                    }
                }
                else
                {
                    _logger.LogError($"HTTP Error for contest {contestId}: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching contest {contestId}");
            }
            
            return problems;
        }

        // NEW: Fetch full problem statement by scraping
        public async Task<CodeforcesProblem> FetchFullProblemStatement(int contestId, string index)
        {
            try
            {
                string url = $"https://codeforces.com/problemset/problem/{contestId}/{index}";
                _logger.LogInformation($"Scraping problem from: {url}");
                
                var web = new HtmlWeb();
                var doc = await web.LoadFromWebAsync(url);
                
                var problem = new CodeforcesProblem
                {
                    ContestId = contestId,
                    Index = index
                };
                
                // Extract problem title
                var titleNode = doc.DocumentNode.SelectSingleNode("//div[@class='title']");
                if (titleNode != null)
                {
                    problem.Name = titleNode.InnerText.Trim();
                }
                
                // Extract problem description
                var descriptionNode = doc.DocumentNode.SelectSingleNode("//div[@class='problem-statement']/div[@class='header']/following-sibling::div[1]");
                if (descriptionNode != null)
                {
                    problem.Description = CleanHtml(descriptionNode.InnerHtml);
                }
                
                // Extract input specification
                var inputNode = doc.DocumentNode.SelectSingleNode("//div[@class='input-specification']");
                if (inputNode != null)
                {
                    problem.InputFormat = CleanHtml(inputNode.InnerHtml);
                }
                
                // Extract output specification
                var outputNode = doc.DocumentNode.SelectSingleNode("//div[@class='output-specification']");
                if (outputNode != null)
                {
                    problem.OutputFormat = CleanHtml(outputNode.InnerHtml);
                }
                
                // Extract sample tests
                var sampleTests = doc.DocumentNode.SelectNodes("//div[@class='sample-test']");
                if (sampleTests != null && sampleTests.Count > 0)
                {
                    var input = sampleTests[0].SelectSingleNode(".//pre");
                    var output = sampleTests[0].SelectSingleNode(".//pre[2]");
                    
                    if (input != null)
                        problem.SampleInput = CleanHtml(input.InnerHtml);
                    if (output != null)
                        problem.SampleOutput = CleanHtml(output.InnerHtml);
                }
                
                // Extract constraints and notes
                var noteNode = doc.DocumentNode.SelectSingleNode("//div[@class='note']");
                if (noteNode != null)
                {
                    problem.Constraints = CleanHtml(noteNode.InnerHtml);
                }
                
                return problem;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error scraping problem {contestId}{index}");
                return null;
            }
        }

        // Helper method to clean HTML
        private string CleanHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return "";
            
            // Remove HTML tags but preserve line breaks
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            
            // Replace <br> with newlines
            foreach (var br in doc.DocumentNode.SelectNodes("//br") ?? Enumerable.Empty<HtmlNode>())
            {
                br.ParentNode.ReplaceChild(HtmlNode.CreateNode("\n"), br);
            }
            
            // Replace <p> with newlines
            foreach (var p in doc.DocumentNode.SelectNodes("//p") ?? Enumerable.Empty<HtmlNode>())
            {
                p.ParentNode.ReplaceChild(HtmlNode.CreateNode(p.InnerText + "\n\n"), p);
            }
            
            // Get text
            string text = doc.DocumentNode.InnerText;
            
            // Clean up extra whitespace
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\n\s+", "\n");
            
            return text.Trim();
        }

        // Save problem to database
        private async Task SaveProblemToDatabase(CodeforcesProblem problem)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                var query = @"
                    INSERT INTO codeforces_problems 
                    (contest_id, problem_index, name, type, points, rating, tags, solved_count, 
                     description, input_format, output_format, sample_input, sample_output, constraints, imported_at)
                    VALUES 
                    (@contestId, @index, @name, @type, @points, @rating, @tags, @solvedCount,
                     @description, @inputFormat, @outputFormat, @sampleInput, @sampleOutput, @constraints, @importedAt)
                    ON CONFLICT (contest_id, problem_index) 
                    DO UPDATE SET 
                        name = EXCLUDED.name,
                        rating = EXCLUDED.rating,
                        tags = EXCLUDED.tags,
                        solved_count = EXCLUDED.solved_count,
                        description = EXCLUDED.description,
                        input_format = EXCLUDED.input_format,
                        output_format = EXCLUDED.output_format,
                        sample_input = EXCLUDED.sample_input,
                        sample_output = EXCLUDED.sample_output,
                        constraints = EXCLUDED.constraints,
                        imported_at = EXCLUDED.imported_at";

                using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@contestId", problem.ContestId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@index", problem.Index ?? "");
                cmd.Parameters.AddWithValue("@name", problem.Name ?? "");
                cmd.Parameters.AddWithValue("@type", problem.Type ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@points", problem.Points ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@rating", problem.Rating ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@tags", problem.Tags?.ToArray() ?? Array.Empty<string>());
                cmd.Parameters.AddWithValue("@solvedCount", problem.SolvedCount ?? 0);
                cmd.Parameters.AddWithValue("@description", problem.Description ?? "");
                cmd.Parameters.AddWithValue("@inputFormat", problem.InputFormat ?? "");
                cmd.Parameters.AddWithValue("@outputFormat", problem.OutputFormat ?? "");
                cmd.Parameters.AddWithValue("@sampleInput", problem.SampleInput ?? "");
                cmd.Parameters.AddWithValue("@sampleOutput", problem.SampleOutput ?? "");
                cmd.Parameters.AddWithValue("@constraints", problem.Constraints ?? "");
                cmd.Parameters.AddWithValue("@importedAt", DateTime.Now);

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving problem {problem.ContestId}{problem.Index} to database");
            }
        }

        // Convert Codeforces problem to your local Problem format
        public Problem ConvertToLocalProblem(CodeforcesProblem cfProblem)
        {
            // Determine difficulty based on rating
            int difficulty = MapRatingToDifficulty(cfProblem.Rating ?? 1200);
            
            // Calculate points based on rating
            int points = cfProblem.Points.HasValue ? (int)cfProblem.Points.Value : (cfProblem.Rating.HasValue ? cfProblem.Rating.Value / 10 : 10);
            
            // Get primary category from tags
            string category = cfProblem.Tags != null && cfProblem.Tags.Count > 0 
                ? cfProblem.Tags.First() 
                : "Codeforces";

            return new Problem
            {
                Title = $"{cfProblem.Name} (CF {cfProblem.ContestId}{cfProblem.Index})",
                Description = cfProblem.Description ?? 
                    $"Imported from Codeforces Round #{cfProblem.ContestId}\n\n" +
                    $"**Original Problem:** [Link](https://codeforces.com/problemset/problem/{cfProblem.ContestId}/{cfProblem.Index})\n\n" +
                    $"**Rating:** {cfProblem.Rating ?? 0}\n" +
                    $"**Tags:** {(cfProblem.Tags != null ? string.Join(", ", cfProblem.Tags) : "None")}",
                InputFormat = cfProblem.InputFormat ?? "Standard input",
                OutputFormat = cfProblem.OutputFormat ?? "Standard output",
                SampleInput = cfProblem.SampleInput ?? "See original problem at Codeforces",
                SampleOutput = cfProblem.SampleOutput ?? "See original problem at Codeforces",
                Constraints = cfProblem.Constraints ?? 
                    $"Time limit: 2 seconds\nMemory limit: 256 MB\nRating: {cfProblem.Rating ?? 0}",
                Difficulty = difficulty,
                Points = points,
                Category = category,
                TimeLimit = 2,
                MemoryLimit = 256,
                CreatedAt = DateTime.Now,
                IsActive = true,
                Source = "Codeforces",
                SourceId = $"{cfProblem.ContestId}{cfProblem.Index}"
            };
        }

        // Map Codeforces rating to local difficulty (1=Easy, 2=Medium, 3=Hard)
        private int MapRatingToDifficulty(int rating)
        {
            if (rating < 1200) return 1; // Easy (Newbie)
            if (rating < 1400) return 1; // Easy (Pupil)
            if (rating < 1600) return 2; // Medium (Specialist)
            if (rating < 1900) return 2; // Medium (Expert)
            if (rating < 2100) return 3; // Hard (Candidate Master)
            if (rating < 2400) return 3; // Hard (Master)
            return 3; // Hard (Grandmaster+)
        }

        // Get recent contests
        public async Task<List<CodeforcesContest>> FetchRecentContests(int count = 10)
        {
            var contests = new List<CodeforcesContest>();
            
            try
            {
                var response = await _httpClient.GetAsync("contest.list?gym=false");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var data = JObject.Parse(json);
                    
                    if (data["status"].ToString() == "OK")
                    {
                        var contestsArray = data["result"] as JArray;
                        
                        foreach (var contest in contestsArray.Take(count))
                        {
                            contests.Add(new CodeforcesContest
                            {
                                Id = contest["id"]?.Value<int>(),
                                Name = contest["name"]?.Value<string>(),
                                Type = contest["type"]?.Value<string>(),
                                Phase = contest["phase"]?.Value<string>(),
                                DurationSeconds = contest["durationSeconds"]?.Value<int?>(),
                                StartTime = DateTimeOffset.FromUnixTimeSeconds(contest["startTimeSeconds"]?.Value<long>() ?? 0).DateTime
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching recent contests");
            }
            
            return contests;
        }
    }

    // Model classes for Codeforces data
    public class CodeforcesProblem
    {
        public int? ContestId { get; set; }
        public string Index { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public double? Points { get; set; }
        public int? Rating { get; set; }
        public List<string> Tags { get; set; }
        public int? SolvedCount { get; set; }
        
        // New properties for full problem statement
        public string Description { get; set; }
        public string InputFormat { get; set; }
        public string OutputFormat { get; set; }
        public string SampleInput { get; set; }
        public string SampleOutput { get; set; }
        public string Constraints { get; set; }
        
        public string FullProblemId => $"{ContestId}{Index}";
        public string ProblemUrl => ContestId.HasValue && !string.IsNullOrEmpty(Index) 
            ? $"https://codeforces.com/problemset/problem/{ContestId}/{Index}"
            : "#";
    }

    public class CodeforcesUser
    {
        public string Handle { get; set; }
        public int? Rating { get; set; }
        public int? MaxRating { get; set; }
        public string Rank { get; set; }
        public string MaxRank { get; set; }
        public int? Contribution { get; set; }
        public int? FriendOfCount { get; set; }
        public DateTime RegistrationTime { get; set; }
    }

    public class CodeforcesSubmission
    {
        public int? Id { get; set; }
        public int? ContestId { get; set; }
        public CodeforcesProblem Problem { get; set; }
        public string Verdict { get; set; }
        public int? TimeConsumed { get; set; } // milliseconds
        public int? MemoryConsumed { get; set; } // bytes
        public string ProgrammingLanguage { get; set; }
        public DateTime SubmissionTime { get; set; }
        
        public string VerdictDisplay 
        { 
            get
            {
                return Verdict switch
                {
                    "OK" => "Accepted",
                    "WRONG_ANSWER" => "Wrong Answer",
                    "TIME_LIMIT_EXCEEDED" => "Time Limit Exceeded",
                    "MEMORY_LIMIT_EXCEEDED" => "Memory Limit Exceeded",
                    "RUNTIME_ERROR" => "Runtime Error",
                    "COMPILATION_ERROR" => "Compilation Error",
                    _ => Verdict ?? "Unknown"
                };
            }
        }
    }

    public class CodeforcesContest
    {
        public int? Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Phase { get; set; }
        public int? DurationSeconds { get; set; }
        public DateTime StartTime { get; set; }
        
        public string DurationDisplay 
        { 
            get
            {
                if (!DurationSeconds.HasValue) return "N/A";
                var hours = DurationSeconds.Value / 3600;
                var minutes = (DurationSeconds.Value % 3600) / 60;
                return $"{hours}h {minutes}m";
            }
        }
    }
}