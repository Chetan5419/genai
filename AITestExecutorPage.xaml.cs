using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using jpmc_genai.Services;

namespace jpmc_genai
{
    public partial class AITestExecutorPage : Page
    {
        private readonly string _projectId;
        private List<TestCase> _allTestCases;
        private string _selectedTestCaseId;
        private string _rawTestPlanJson;
        private readonly List<string> _executionLogs = new List<string>();

        public AITestExecutorPage(string projectId)
        {
            InitializeComponent();
            _projectId = projectId;
            LoadProjectDetails();
            LoadTestCases();
        }

        private void LoadProjectDetails()
        {
            var project = Session.CurrentUser?.projects?.FirstOrDefault(p => p.projectid == _projectId);
            ProjectTitleTextBlock.Text = project?.title ?? "Unknown Project";
            ProjectDetailsTextBlock.Text = $"ID: {_projectId}\nType: {project?.projecttype ?? "N/A"}\nStarted: {project?.startdate ?? "N/A"}";
        }

        private async void LoadTestCases()
        {
            try
            {
                UpdateExecutionLog("[v0] Loading test cases from API...");
                using var client = new ApiClient();
                client.SetBearer(Session.Token);
                
                var response = await client.GetAsync($"projects/{_projectId}/testcases");
                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    UpdateExecutionLog("[v0] Raw API response: " + jsonContent.Substring(0, Math.Min(500, jsonContent.Length)));
                    
                    _allTestCases = JsonSerializer.Deserialize<List<TestCase>>(jsonContent, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    if (_allTestCases != null && _allTestCases.Count > 0)
                    {
                        TestCaseComboBox.ItemsSource = _allTestCases.Select(tc => tc.testcaseid).ToList();
                        UpdateExecutionLog($"[v0] ✓ Loaded {_allTestCases.Count} test cases");
                        TestPlanJsonTextBox.Text = "Test cases loaded. Select one and click 'Load Plan'";
                    }
                    else
                    {
                        UpdateExecutionLog("[v0] ✗ No test cases found in response");
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    UpdateExecutionLog($"[v0] ✗ Failed to load test cases: {response.StatusCode}\n{errorContent}");
                }
            }
            catch (Exception ex)
            {
                UpdateExecutionLog($"[v0] ✗ Error loading test cases: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private async void LoadTestPlanButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedTestCase = TestCaseComboBox.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedTestCase))
            {
                UpdateExecutionLog("[v0] ✗ No test case selected");
                TestPlanJsonTextBox.Text = "ERROR: Please select a test case first";
                return;
            }

            _selectedTestCaseId = selectedTestCase;
            await FetchAndDisplayTestPlan(selectedTestCase);
        }

        private async System.Threading.Tasks.Task FetchAndDisplayTestPlan(string testCaseId)
        {
            try
            {
                UpdateExecutionLog($"[v0] Fetching test plan for {testCaseId}...");
                using var client = new ApiClient();
                client.SetBearer(Session.Token);
                
                var response = await client.GetAsync($"testplan/{testCaseId}");
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    _rawTestPlanJson = jsonContent;
                    
                    UpdateExecutionLog($"[v0] ✓ Test plan fetched successfully");
                    
                    try
                    {
                        var jsonDoc = JsonDocument.Parse(jsonContent);
                        var prettyJson = JsonSerializer.Serialize(jsonDoc.RootElement, 
                            new JsonSerializerOptions { WriteIndented = true });
                        TestPlanJsonTextBox.Text = prettyJson;
                    }
                    catch
                    {
                        TestPlanJsonTextBox.Text = jsonContent;
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    UpdateExecutionLog($"[v0] ✗ Failed to fetch test plan: {response.StatusCode}");
                    TestPlanJsonTextBox.Text = $"ERROR {response.StatusCode}:\n{errorContent}";
                }
            }
            catch (Exception ex)
            {
                UpdateExecutionLog($"[v0] ✗ Error fetching test plan: {ex.Message}");
                TestPlanJsonTextBox.Text = $"ERROR: {ex.Message}\n{ex.StackTrace}";
            }
        }

        private async System.Threading.Tasks.Task GenerateAndExecuteTest(string testCaseId)
        {
            try
            {
                using var client = new ApiClient();
                client.SetBearer(Session.Token);
                
                // Step 1: Build the request payload from test plan JSON
                UpdateExecutionLog("[STEP 1] Building request payload from test plan...");
                if (string.IsNullOrEmpty(_rawTestPlanJson))
                {
                    UpdateExecutionLog("✗ Test plan not loaded. Please load test plan first.");
                    return;
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var testPlan = JsonSerializer.Deserialize<TestPlan>(_rawTestPlanJson, options);
                
                // Build the request object with keys that use spaces/dashes as per API requirement
                var requestPayload = new Dictionary<string, object>
                {
                    { "pretestid - steps", testPlan?.pretestid_steps ?? new Dictionary<string, Dictionary<string, string>>() },
                    { "pretestid - scripts", testPlan?.pretestid_scripts ?? new Dictionary<string, string>() },
                    { "current testid", testCaseId },
                    { "current - bdd steps", testPlan?.current_bdd_steps ?? new Dictionary<string, string>() }
                };

                var requestJson = JsonSerializer.Serialize(requestPayload, new JsonSerializerOptions { WriteIndented = true });
                UpdateExecutionLog($"[v0] Request payload:\n{requestJson}");
                
                // Step 2: Generate script with proper payload
                UpdateExecutionLog("[STEP 2] Sending request to generate-test-script...");
                var generateUrl = $"generate-test-script/{testCaseId}?script_type=playwright&script_lang=python";
                var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
                var generateResponse = await client.PostAsync(generateUrl, content);
                
                if (!generateResponse.IsSuccessStatusCode)
                {
                    var errorContent = await generateResponse.Content.ReadAsStringAsync();
                    UpdateExecutionLog($"✗ Script generation failed: {generateResponse.StatusCode}\n{errorContent}");
                    return;
                }
                
                var scriptContent = await generateResponse.Content.ReadAsStringAsync();
                UpdateExecutionLog($"✓ Script generated ({scriptContent.Length} bytes)");
                UpdateExecutionLog($"[v0] Script preview:\n{scriptContent.Substring(0, Math.Min(300, scriptContent.Length))}...");
                
                // Clean markdown code blocks
                scriptContent = Regex.Replace(scriptContent, @"^\`\`\`(?:python)?\s*\n?", "", RegexOptions.Multiline);
                scriptContent = Regex.Replace(scriptContent, @"\n?\s*\`\`\`$", "", RegexOptions.Multiline);
                
                var tempScriptPath = Path.Combine(Path.GetTempPath(), $"{testCaseId}_{DateTime.Now.Ticks}.py");
                File.WriteAllText(tempScriptPath, scriptContent);
                UpdateExecutionLog($"✓ Script saved: {Path.GetFileName(tempScriptPath)}");
                
                // Step 3: Execute script
                UpdateExecutionLog("[STEP 3] Uploading and executing script...");
                var fileContent = File.ReadAllBytes(tempScriptPath);
                var formData = new MultipartFormDataContent
                {
                    { new ByteArrayContent(fileContent), "file", Path.GetFileName(tempScriptPath) }
                };
                
                var executeResponse = await client.PostAsync("execute-code?script_type=playwright", formData);
                
                if (executeResponse.IsSuccessStatusCode)
                {
                    var executionOutput = await executeResponse.Content.ReadAsStringAsync();
                    UpdateExecutionLog($"✓ Script executed successfully\n");
                    UpdateExecutionLog($"--- EXECUTION OUTPUT ---\n{executionOutput}\n--- END OUTPUT ---");
                }
                else
                {
                    var errorContent = await executeResponse.Content.ReadAsStringAsync();
                    UpdateExecutionLog($"✗ Execution failed: {executeResponse.StatusCode}\n{errorContent}");
                }
                
                // Cleanup
                try { File.Delete(tempScriptPath); } catch { }
            }
            catch (Exception ex)
            {
                UpdateExecutionLog($"✗ Error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void UpdateExecutionLog(string logMessage)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            _executionLogs.Add($"[{timestamp}] {logMessage}");
            ExecutionLogTextBox.Text = string.Join("\n", _executionLogs);
            ExecutionLogTextBox.ScrollToEnd();
        }

        private void ClearExecutionLog_Click(object sender, RoutedEventArgs e)
        {
            _executionLogs.Clear();
            ExecutionLogTextBox.Clear();
        }

        private void BackToDashboard_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new DashboardPage(_projectId));
        }

        private void ScriptGenerator_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new ScriptGeneratorPage());
        }

        private void ExecutionLog_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new ExecutionLogPage());
        }

        private void UploadTestCase_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new UploadTestCasePage());
        }

        private void ChangeProject_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new ProjectPage());
        }

        private async void ExecuteButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedTestCaseId))
            {
                UpdateExecutionLog("[v0] ✗ No test case selected. Please load a test plan first.");
                return;
            }
            
            UpdateExecutionLog("\n========== EXECUTION START ==========");
            await GenerateAndExecuteTest(_selectedTestCaseId);
            UpdateExecutionLog("========== EXECUTION END ==========\n");
        }
    }
}
