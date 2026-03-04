using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System;
using MyMvcApp.Models;

public class CompilerController : Controller
{
    // GET: /Compiler
    public IActionResult Index(int? problemId = null)
    {
        ViewBag.SelectedLanguage = 54; // Default to C++
        
        // If problemId is provided, load problem info
        if (problemId.HasValue)
        {
            // Try to get from TempData first (if redirected from Problem page)
            if (TempData["ProblemTitle"] != null)
            {
                ViewBag.ProblemTitle = TempData["ProblemTitle"];
                ViewBag.ProblemDescription = TempData["ProblemDescription"];
                ViewBag.ProblemSampleInput = TempData["ProblemSampleInput"];
                ViewBag.ProblemSampleOutput = TempData["ProblemSampleOutput"];
                ViewBag.ProblemId = problemId;
                
                // Keep TempData for next request
                TempData.Keep("ProblemTitle");
                TempData.Keep("ProblemDescription");
                TempData.Keep("ProblemSampleInput");
                TempData.Keep("ProblemSampleOutput");
                TempData.Keep("ProblemId");
            }
        }
        
        return View();
    }

    // POST: /Compiler
    [HttpPost]
    public async Task<IActionResult> Index(string sourceCode, int languageId = 54)
    {
        // Store the submitted code and selected language in ViewBag
        ViewBag.SubmittedCode = sourceCode;
        ViewBag.SelectedLanguage = languageId;
        
        // Restore problem context from TempData
        if (TempData["ProblemTitle"] != null)
        {
            ViewBag.ProblemTitle = TempData["ProblemTitle"];
            ViewBag.ProblemDescription = TempData["ProblemDescription"];
            ViewBag.ProblemSampleInput = TempData["ProblemSampleInput"];
            ViewBag.ProblemSampleOutput = TempData["ProblemSampleOutput"];
            ViewBag.ProblemId = TempData["ProblemId"];
            
            // Keep TempData for next request
            TempData.Keep("ProblemTitle");
            TempData.Keep("ProblemDescription");
            TempData.Keep("ProblemSampleInput");
            TempData.Keep("ProblemSampleOutput");
            TempData.Keep("ProblemId");
        }
        
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            ViewBag.Output = "Error: No code provided";
            return View();
        }

        using (var client = new HttpClient())
        {
            try
            {
                client.Timeout = TimeSpan.FromSeconds(30);

                var requestBody = new
                {
                    language_id = languageId,
                    source_code = sourceCode,
                    stdin = "",
                    cpu_time_limit = 2,
                    memory_limit = 256000
                };

                var content = new StringContent(
                    JsonConvert.SerializeObject(requestBody),
                    Encoding.UTF8,
                    "application/json"
                );

                var submitResponse = await client.PostAsync(
                    "https://ce.judge0.com/submissions?base64_encoded=false&wait=false",
                    content
                );

                if (!submitResponse.IsSuccessStatusCode)
                {
                    ViewBag.Output = $"API Error: {submitResponse.StatusCode}";
                    return View();
                }

                var submitResult = await submitResponse.Content.ReadAsStringAsync();
                dynamic submitJson = JsonConvert.DeserializeObject(submitResult);

                if (submitJson == null || submitJson.token == null)
                {
                    ViewBag.Output = "Error: Failed to get submission token";
                    return View();
                }

                string token = submitJson.token;
                int maxAttempts = 10;
                int attempt = 0;
                bool completed = false;
                dynamic finalJson = null;

                while (!completed && attempt < maxAttempts)
                {
                    await Task.Delay(2000);

                    var getResponse = await client.GetAsync(
                        $"https://ce.judge0.com/submissions/{token}?base64_encoded=false"
                    );

                    if (getResponse.IsSuccessStatusCode)
                    {
                        var finalResult = await getResponse.Content.ReadAsStringAsync();
                        finalJson = JsonConvert.DeserializeObject(finalResult);

                        if (finalJson != null && finalJson.status != null)
                        {
                            int currentStatusId = finalJson.status.id;
                            if (currentStatusId >= 3)
                            {
                                completed = true;
                                break;
                            }
                        }
                    }
                    attempt++;
                }

                if (finalJson == null)
                {
                    ViewBag.Output = "Error: No response from judge server";
                    return View();
                }

                int resultStatusId = finalJson.status?.id ?? 0;
                string statusDescription = finalJson.status?.description ?? "Unknown";

                string output = "";
                switch (resultStatusId)
                {
                    case 3:
                        output = finalJson.stdout ?? "No output";
                        break;
                    case 4:
                        output = $"Wrong Answer\n\nYour Output:\n{finalJson.stdout}\n\nExpected:\n{finalJson.expected_output}";
                        break;
                    case 5:
                        output = "⏰ Time Limit Exceeded (TLE)";
                        break;
                    case 6:
                        output = "💾 Memory Limit Exceeded (MLE)";
                        break;
                    case 7:
                        output = $"Runtime Error:\n{finalJson.stderr ?? finalJson.message ?? "Unknown error"}";
                        break;
                    case 8:
                        output = $"Compilation Error:\n{finalJson.compile_output ?? "Compilation failed"}";
                        break;
                    default:
                        output = finalJson.stdout 
                                ?? finalJson.stderr 
                                ?? finalJson.compile_output 
                                ?? $"Status: {statusDescription}";
                        break;
                }

                ViewBag.Output = output;
                ViewBag.StatusCode = resultStatusId;
                ViewBag.StatusDescription = statusDescription;
                ViewBag.Time = finalJson.time != null ? $"{finalJson.time} s" : "N/A";
                ViewBag.Memory = finalJson.memory != null ? $"{finalJson.memory} KB" : "N/A";
            }
            catch (Exception ex)
            {
                ViewBag.Output = $"Error: {ex.Message}";
            }
        }

        return View();
    }
}