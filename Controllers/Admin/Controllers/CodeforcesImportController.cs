// Controllers/Admin/CodeforcesImportController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMvcApp.Services;
using Npgsql;
using System.Text.Json;

namespace MyMvcApp.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/Codeforces")]
    public class CodeforcesImportController : Controller
    {
        private readonly CodeforcesService _codeforcesService;
        private readonly string _connectionString;
        private readonly ILogger<CodeforcesImportController> _logger;
        private readonly IConfiguration _configuration;

        public CodeforcesImportController(CodeforcesService codeforcesService,
                                          IConfiguration configuration,
                                          ILogger<CodeforcesImportController> logger)
        {
            _codeforcesService = codeforcesService;
            _configuration = configuration;
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _logger = logger;
        }

        [HttpGet]
public IActionResult Index()
{
    // Mask the API key for display
    var apiKey = _configuration["Codeforces:ApiKey"];
    if (!string.IsNullOrEmpty(apiKey) && apiKey.Length > 12)
    {
        ViewBag.MaskedApiKey = apiKey.Substring(0, 8) + "..." + apiKey.Substring(apiKey.Length - 4);
    }
    else
    {
        ViewBag.MaskedApiKey = "Not configured";
    }
    
    // Specify the full path to the view
    return View("~/Views/Admin/CodeforcesImport/Index.cshtml");
}

        [HttpPost("import-all")]
        public async Task<IActionResult> ImportAllProblems(int limit = 50)
        {
            try
            {
                _logger.LogInformation($"Starting import of {limit} problems from Codeforces");
                var problems = await _codeforcesService.FetchAllProblems(limit);
                
                int imported = 0;
                int skipped = 0;
                int failed = 0;
                
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                int count = 0;
                foreach (var cfProblem in problems)
                {
                    count++;
                    try
                    {
                        _logger.LogInformation($"Processing problem {count}/{problems.Count}: {cfProblem.ContestId}{cfProblem.Index}");
                        
                        // Add delay to be respectful to Codeforces servers
                        if (count > 1) await Task.Delay(1000);
                        
                        // Scrape full problem details
                        if (cfProblem.ContestId.HasValue && !string.IsNullOrEmpty(cfProblem.Index))
                        {
                            var fullProblem = await _codeforcesService.FetchFullProblemStatement(
                                cfProblem.ContestId.Value, cfProblem.Index);
                            
                            if (fullProblem != null)
                            {
                                cfProblem.Description = fullProblem.Description;
                                cfProblem.InputFormat = fullProblem.InputFormat;
                                cfProblem.OutputFormat = fullProblem.OutputFormat;
                                cfProblem.SampleInput = fullProblem.SampleInput;
                                cfProblem.SampleOutput = fullProblem.SampleOutput;
                                cfProblem.Constraints = fullProblem.Constraints;
                            }
                        }
                        
                        var localProblem = _codeforcesService.ConvertToLocalProblem(cfProblem);
                        
                        // Check if already exists
                        var checkQuery = "SELECT COUNT(*) FROM problems WHERE source = 'Codeforces' AND source_id = @sourceId";
                        using var checkCmd = new NpgsqlCommand(checkQuery, conn);
                        checkCmd.Parameters.AddWithValue("@sourceId", localProblem.SourceId ?? "");
                        var exists = (long)await checkCmd.ExecuteScalarAsync() > 0;
                        
                        if (!exists)
                        {
                            var insertQuery = @"
                                INSERT INTO problems 
                                (title, description, input_format, output_format, sample_input, sample_output, 
                                 constraints, difficulty, points, category, time_limit, memory_limit, 
                                 created_at, is_active, source, source_id)
                                VALUES 
                                (@title, @description, @inputFormat, @outputFormat, @sampleInput, @sampleOutput,
                                 @constraints, @difficulty, @points, @category, @timeLimit, @memoryLimit,
                                 @createdAt, @isActive, @source, @sourceId)";

                            using var insertCmd = new NpgsqlCommand(insertQuery, conn);
                            insertCmd.Parameters.AddWithValue("@title", localProblem.Title ?? "");
                            insertCmd.Parameters.AddWithValue("@description", localProblem.Description ?? "");
                            insertCmd.Parameters.AddWithValue("@inputFormat", localProblem.InputFormat ?? "");
                            insertCmd.Parameters.AddWithValue("@outputFormat", localProblem.OutputFormat ?? "");
                            insertCmd.Parameters.AddWithValue("@sampleInput", localProblem.SampleInput ?? "");
                            insertCmd.Parameters.AddWithValue("@sampleOutput", localProblem.SampleOutput ?? "");
                            insertCmd.Parameters.AddWithValue("@constraints", localProblem.Constraints ?? "");
                            insertCmd.Parameters.AddWithValue("@difficulty", localProblem.Difficulty);
                            insertCmd.Parameters.AddWithValue("@points", localProblem.Points);
                            insertCmd.Parameters.AddWithValue("@category", localProblem.Category ?? "Codeforces");
                            insertCmd.Parameters.AddWithValue("@timeLimit", localProblem.TimeLimit);
                            insertCmd.Parameters.AddWithValue("@memoryLimit", localProblem.MemoryLimit);
                            insertCmd.Parameters.AddWithValue("@createdAt", localProblem.CreatedAt);
                            insertCmd.Parameters.AddWithValue("@isActive", localProblem.IsActive);
                            insertCmd.Parameters.AddWithValue("@source", "Codeforces");
                            insertCmd.Parameters.AddWithValue("@sourceId", localProblem.SourceId ?? "");

                            await insertCmd.ExecuteNonQueryAsync();
                            imported++;
                            _logger.LogInformation($"Imported: {localProblem.Title}");
                        }
                        else
                        {
                            skipped++;
                            _logger.LogInformation($"Skipped (already exists): {localProblem.Title}");
                        }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        _logger.LogError(ex, $"Failed to import problem {cfProblem.ContestId}{cfProblem.Index}");
                    }
                }

                TempData["SuccessMessage"] = $"Successfully imported {imported} new problems from Codeforces. Skipped {skipped} existing problems. Failed: {failed}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in import all");
                TempData["ErrorMessage"] = $"Error importing problems: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost("import-contest")]
        public async Task<IActionResult> ImportContest(int contestId)
        {
            try
            {
                _logger.LogInformation($"Importing contest {contestId} from Codeforces");
                var problems = await _codeforcesService.FetchContestProblems(contestId);
                
                int imported = 0;
                int skipped = 0;
                int failed = 0;
                
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                int count = 0;
                foreach (var cfProblem in problems)
                {
                    count++;
                    try
                    {
                        _logger.LogInformation($"Processing problem {count}/{problems.Count}: {cfProblem.ContestId}{cfProblem.Index}");
                        
                        // Add delay to be respectful to Codeforces servers
                        if (count > 1) await Task.Delay(1000);
                        
                        // Scrape full problem details
                        if (cfProblem.ContestId.HasValue && !string.IsNullOrEmpty(cfProblem.Index))
                        {
                            var fullProblem = await _codeforcesService.FetchFullProblemStatement(
                                cfProblem.ContestId.Value, cfProblem.Index);
                            
                            if (fullProblem != null)
                            {
                                cfProblem.Description = fullProblem.Description;
                                cfProblem.InputFormat = fullProblem.InputFormat;
                                cfProblem.OutputFormat = fullProblem.OutputFormat;
                                cfProblem.SampleInput = fullProblem.SampleInput;
                                cfProblem.SampleOutput = fullProblem.SampleOutput;
                                cfProblem.Constraints = fullProblem.Constraints;
                            }
                        }
                        
                        var localProblem = _codeforcesService.ConvertToLocalProblem(cfProblem);
                        
                        // Check if already exists
                        var checkQuery = "SELECT COUNT(*) FROM problems WHERE source = 'Codeforces' AND source_id = @sourceId";
                        using var checkCmd = new NpgsqlCommand(checkQuery, conn);
                        checkCmd.Parameters.AddWithValue("@sourceId", localProblem.SourceId ?? "");
                        var exists = (long)await checkCmd.ExecuteScalarAsync() > 0;
                        
                        if (!exists)
                        {
                            var insertQuery = @"
                                INSERT INTO problems 
                                (title, description, input_format, output_format, sample_input, sample_output, 
                                 constraints, difficulty, points, category, time_limit, memory_limit, 
                                 created_at, is_active, source, source_id)
                                VALUES 
                                (@title, @description, @inputFormat, @outputFormat, @sampleInput, @sampleOutput,
                                 @constraints, @difficulty, @points, @category, @timeLimit, @memoryLimit,
                                 @createdAt, @isActive, @source, @sourceId)";

                            using var insertCmd = new NpgsqlCommand(insertQuery, conn);
                            insertCmd.Parameters.AddWithValue("@title", localProblem.Title ?? "");
                            insertCmd.Parameters.AddWithValue("@description", localProblem.Description ?? "");
                            insertCmd.Parameters.AddWithValue("@inputFormat", localProblem.InputFormat ?? "");
                            insertCmd.Parameters.AddWithValue("@outputFormat", localProblem.OutputFormat ?? "");
                            insertCmd.Parameters.AddWithValue("@sampleInput", localProblem.SampleInput ?? "");
                            insertCmd.Parameters.AddWithValue("@sampleOutput", localProblem.SampleOutput ?? "");
                            insertCmd.Parameters.AddWithValue("@constraints", localProblem.Constraints ?? "");
                            insertCmd.Parameters.AddWithValue("@difficulty", localProblem.Difficulty);
                            insertCmd.Parameters.AddWithValue("@points", localProblem.Points);
                            insertCmd.Parameters.AddWithValue("@category", localProblem.Category ?? "Codeforces");
                            insertCmd.Parameters.AddWithValue("@timeLimit", localProblem.TimeLimit);
                            insertCmd.Parameters.AddWithValue("@memoryLimit", localProblem.MemoryLimit);
                            insertCmd.Parameters.AddWithValue("@createdAt", localProblem.CreatedAt);
                            insertCmd.Parameters.AddWithValue("@isActive", localProblem.IsActive);
                            insertCmd.Parameters.AddWithValue("@source", "Codeforces");
                            insertCmd.Parameters.AddWithValue("@sourceId", localProblem.SourceId ?? "");

                            await insertCmd.ExecuteNonQueryAsync();
                            imported++;
                            _logger.LogInformation($"Imported: {localProblem.Title}");
                        }
                        else
                        {
                            skipped++;
                            _logger.LogInformation($"Skipped (already exists): {localProblem.Title}");
                        }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        _logger.LogError(ex, $"Failed to import problem {cfProblem.ContestId}{cfProblem.Index}");
                    }
                }

                TempData["SuccessMessage"] = $"Imported {imported} problems from contest {contestId}. Skipped {skipped} existing problems. Failed: {failed}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing contest");
                TempData["ErrorMessage"] = $"Error importing contest: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost("import-by-rating")]
        public async Task<IActionResult> ImportByRating(int minRating, int maxRating)
        {
            try
            {
                _logger.LogInformation($"Importing problems with rating {minRating}-{maxRating}");
                var problems = await _codeforcesService.FetchProblemsByRating(minRating, maxRating, 50);
                
                int imported = 0;
                int skipped = 0;
                int failed = 0;
                
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                int count = 0;
                foreach (var cfProblem in problems)
                {
                    count++;
                    try
                    {
                        _logger.LogInformation($"Processing problem {count}/{problems.Count}: {cfProblem.ContestId}{cfProblem.Index}");
                        
                        // Add delay to be respectful to Codeforces servers
                        if (count > 1) await Task.Delay(1000);
                        
                        // Scrape full problem details
                        if (cfProblem.ContestId.HasValue && !string.IsNullOrEmpty(cfProblem.Index))
                        {
                            var fullProblem = await _codeforcesService.FetchFullProblemStatement(
                                cfProblem.ContestId.Value, cfProblem.Index);
                            
                            if (fullProblem != null)
                            {
                                cfProblem.Description = fullProblem.Description;
                                cfProblem.InputFormat = fullProblem.InputFormat;
                                cfProblem.OutputFormat = fullProblem.OutputFormat;
                                cfProblem.SampleInput = fullProblem.SampleInput;
                                cfProblem.SampleOutput = fullProblem.SampleOutput;
                                cfProblem.Constraints = fullProblem.Constraints;
                            }
                        }
                        
                        var localProblem = _codeforcesService.ConvertToLocalProblem(cfProblem);
                        
                        // Check if already exists
                        var checkQuery = "SELECT COUNT(*) FROM problems WHERE source = 'Codeforces' AND source_id = @sourceId";
                        using var checkCmd = new NpgsqlCommand(checkQuery, conn);
                        checkCmd.Parameters.AddWithValue("@sourceId", localProblem.SourceId ?? "");
                        var exists = (long)await checkCmd.ExecuteScalarAsync() > 0;
                        
                        if (!exists)
                        {
                            var insertQuery = @"
                                INSERT INTO problems 
                                (title, description, input_format, output_format, sample_input, sample_output, 
                                 constraints, difficulty, points, category, time_limit, memory_limit, 
                                 created_at, is_active, source, source_id)
                                VALUES 
                                (@title, @description, @inputFormat, @outputFormat, @sampleInput, @sampleOutput,
                                 @constraints, @difficulty, @points, @category, @timeLimit, @memoryLimit,
                                 @createdAt, @isActive, @source, @sourceId)";

                            using var insertCmd = new NpgsqlCommand(insertQuery, conn);
                            insertCmd.Parameters.AddWithValue("@title", localProblem.Title ?? "");
                            insertCmd.Parameters.AddWithValue("@description", localProblem.Description ?? "");
                            insertCmd.Parameters.AddWithValue("@inputFormat", localProblem.InputFormat ?? "");
                            insertCmd.Parameters.AddWithValue("@outputFormat", localProblem.OutputFormat ?? "");
                            insertCmd.Parameters.AddWithValue("@sampleInput", localProblem.SampleInput ?? "");
                            insertCmd.Parameters.AddWithValue("@sampleOutput", localProblem.SampleOutput ?? "");
                            insertCmd.Parameters.AddWithValue("@constraints", localProblem.Constraints ?? "");
                            insertCmd.Parameters.AddWithValue("@difficulty", localProblem.Difficulty);
                            insertCmd.Parameters.AddWithValue("@points", localProblem.Points);
                            insertCmd.Parameters.AddWithValue("@category", localProblem.Category ?? "Codeforces");
                            insertCmd.Parameters.AddWithValue("@timeLimit", localProblem.TimeLimit);
                            insertCmd.Parameters.AddWithValue("@memoryLimit", localProblem.MemoryLimit);
                            insertCmd.Parameters.AddWithValue("@createdAt", localProblem.CreatedAt);
                            insertCmd.Parameters.AddWithValue("@isActive", localProblem.IsActive);
                            insertCmd.Parameters.AddWithValue("@source", "Codeforces");
                            insertCmd.Parameters.AddWithValue("@sourceId", localProblem.SourceId ?? "");

                            await insertCmd.ExecuteNonQueryAsync();
                            imported++;
                            _logger.LogInformation($"Imported: {localProblem.Title}");
                        }
                        else
                        {
                            skipped++;
                            _logger.LogInformation($"Skipped (already exists): {localProblem.Title}");
                        }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        _logger.LogError(ex, $"Failed to import problem {cfProblem.ContestId}{cfProblem.Index}");
                    }
                }

                TempData["SuccessMessage"] = $"Imported {imported} problems with rating {minRating}-{maxRating}. Skipped {skipped} existing problems. Failed: {failed}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing by rating");
                TempData["ErrorMessage"] = $"Error importing: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchProblems(string query, int? minRating, int? maxRating)
        {
            try
            {
                var problems = await _codeforcesService.FetchAllProblems(100);
                
                if (!string.IsNullOrEmpty(query))
                {
                    problems = problems.Where(p => 
                        (p.Name != null && p.Name.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                        (p.Tags != null && p.Tags.Any(t => t != null && t.Contains(query, StringComparison.OrdinalIgnoreCase)))
                    ).ToList();
                }
                
                if (minRating.HasValue)
                {
                    problems = problems.Where(p => p.Rating >= minRating).ToList();
                }
                
                if (maxRating.HasValue)
                {
                    problems = problems.Where(p => p.Rating <= maxRating).ToList();
                }
                
                var result = problems.Take(50).Select(p => new
                {
                    p.ContestId,
                    p.Index,
                    p.Name,
                    p.Rating,
                    Tags = p.Tags ?? new List<string>(),
                    p.SolvedCount,
                    ProblemUrl = p.ProblemUrl
                });
                
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching problems");
                return Json(new { error = ex.Message });
            }
        }

        [HttpPost("import-selected")]
        public async Task<IActionResult> ImportSelectedProblems([FromBody] List<string> problemIds)
        {
            try
            {
                _logger.LogInformation($"Importing {problemIds.Count} selected problems");
                
                int imported = 0;
                int skipped = 0;
                int failed = 0;
                
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                int count = 0;
                foreach (var problemId in problemIds)
                {
                    count++;
                    try
                    {
                        // Parse problemId (format: "1851A")
                        var match = System.Text.RegularExpressions.Regex.Match(problemId, @"(\d+)([A-Z]\d*)");
                        if (!match.Success)
                        {
                            _logger.LogWarning($"Invalid problem ID format: {problemId}");
                            failed++;
                            continue;
                        }
                        
                        int contestId = int.Parse(match.Groups[1].Value);
                        string index = match.Groups[2].Value;
                        
                        _logger.LogInformation($"Processing problem {count}/{problemIds.Count}: {contestId}{index}");
                        
                        // Add delay to be respectful to Codeforces servers
                        if (count > 1) await Task.Delay(1000);
                        
                        // Fetch full problem details
                        var fullProblem = await _codeforcesService.FetchFullProblemStatement(contestId, index);
                        
                        if (fullProblem == null)
                        {
                            _logger.LogWarning($"Could not fetch problem {contestId}{index}");
                            failed++;
                            continue;
                        }
                        
                        var localProblem = _codeforcesService.ConvertToLocalProblem(fullProblem);
                        
                        // Check if already exists
                        var checkQuery = "SELECT COUNT(*) FROM problems WHERE source = 'Codeforces' AND source_id = @sourceId";
                        using var checkCmd = new NpgsqlCommand(checkQuery, conn);
                        checkCmd.Parameters.AddWithValue("@sourceId", localProblem.SourceId ?? "");
                        var exists = (long)await checkCmd.ExecuteScalarAsync() > 0;
                        
                        if (!exists)
                        {
                            var insertQuery = @"
                                INSERT INTO problems 
                                (title, description, input_format, output_format, sample_input, sample_output, 
                                 constraints, difficulty, points, category, time_limit, memory_limit, 
                                 created_at, is_active, source, source_id)
                                VALUES 
                                (@title, @description, @inputFormat, @outputFormat, @sampleInput, @sampleOutput,
                                 @constraints, @difficulty, @points, @category, @timeLimit, @memoryLimit,
                                 @createdAt, @isActive, @source, @sourceId)";

                            using var insertCmd = new NpgsqlCommand(insertQuery, conn);
                            insertCmd.Parameters.AddWithValue("@title", localProblem.Title ?? "");
                            insertCmd.Parameters.AddWithValue("@description", localProblem.Description ?? "");
                            insertCmd.Parameters.AddWithValue("@inputFormat", localProblem.InputFormat ?? "");
                            insertCmd.Parameters.AddWithValue("@outputFormat", localProblem.OutputFormat ?? "");
                            insertCmd.Parameters.AddWithValue("@sampleInput", localProblem.SampleInput ?? "");
                            insertCmd.Parameters.AddWithValue("@sampleOutput", localProblem.SampleOutput ?? "");
                            insertCmd.Parameters.AddWithValue("@constraints", localProblem.Constraints ?? "");
                            insertCmd.Parameters.AddWithValue("@difficulty", localProblem.Difficulty);
                            insertCmd.Parameters.AddWithValue("@points", localProblem.Points);
                            insertCmd.Parameters.AddWithValue("@category", localProblem.Category ?? "Codeforces");
                            insertCmd.Parameters.AddWithValue("@timeLimit", localProblem.TimeLimit);
                            insertCmd.Parameters.AddWithValue("@memoryLimit", localProblem.MemoryLimit);
                            insertCmd.Parameters.AddWithValue("@createdAt", localProblem.CreatedAt);
                            insertCmd.Parameters.AddWithValue("@isActive", localProblem.IsActive);
                            insertCmd.Parameters.AddWithValue("@source", "Codeforces");
                            insertCmd.Parameters.AddWithValue("@sourceId", localProblem.SourceId ?? "");

                            await insertCmd.ExecuteNonQueryAsync();
                            imported++;
                            _logger.LogInformation($"Imported: {localProblem.Title}");
                        }
                        else
                        {
                            skipped++;
                            _logger.LogInformation($"Skipped (already exists): {localProblem.Title}");
                        }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        _logger.LogError(ex, $"Failed to import problem {problemId}");
                    }
                }

                return Json(new { 
                    success = true, 
                    imported = imported, 
                    skipped = skipped, 
                    failed = failed,
                    message = $"Imported {imported} problems. Skipped {skipped}. Failed {failed}."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing selected problems");
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetImportStats()
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                // Get stats from codeforces_problems table
                var statsQuery = @"
                    SELECT 
                        COUNT(*) as total_imported,
                        COUNT(DISTINCT contest_id) as total_contests,
                        AVG(rating) as avg_rating,
                        MIN(rating) as min_rating,
                        MAX(rating) as max_rating,
                        COUNT(CASE WHEN rating < 1200 THEN 1 END) as easy_count,
                        COUNT(CASE WHEN rating >= 1200 AND rating < 1600 THEN 1 END) as medium_count,
                        COUNT(CASE WHEN rating >= 1600 THEN 1 END) as hard_count
                    FROM codeforces_problems";

                using var cmd = new NpgsqlCommand(statsQuery, conn);
                using var reader = await cmd.ExecuteReaderAsync();
                
                if (await reader.ReadAsync())
                {
                    var stats = new
                    {
                        TotalImported = reader.GetInt64(0),
                        TotalContests = reader.GetInt64(1),
                        AvgRating = reader.IsDBNull(2) ? 0 : Math.Round(reader.GetDouble(2), 0),
                        MinRating = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                        MaxRating = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                        EasyCount = reader.GetInt64(5),
                        MediumCount = reader.GetInt64(6),
                        HardCount = reader.GetInt64(7)
                    };
                    
                    return Json(stats);
                }
                
                return Json(new { TotalImported = 0 });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting import stats");
                return Json(new { error = ex.Message });
            }
        }

        [HttpPost("clear-all")]
        public async Task<IActionResult> ClearAllImported()
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                // Delete from main problems table
                var deleteProblemsQuery = "DELETE FROM problems WHERE source = 'Codeforces'";
                using var deleteProblemsCmd = new NpgsqlCommand(deleteProblemsQuery, conn);
                var problemsDeleted = await deleteProblemsCmd.ExecuteNonQueryAsync();

                // Delete from codeforces_problems table
                var deleteCodeforcesQuery = "DELETE FROM codeforces_problems";
                using var deleteCodeforcesCmd = new NpgsqlCommand(deleteCodeforcesQuery, conn);
                var codeforcesDeleted = await deleteCodeforcesCmd.ExecuteNonQueryAsync();

                TempData["SuccessMessage"] = $"Cleared {problemsDeleted} problems from main table and {codeforcesDeleted} from Codeforces table.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing imported problems");
                TempData["ErrorMessage"] = $"Error clearing problems: {ex.Message}";
            }

            return RedirectToAction("Index");
        }
    }
}