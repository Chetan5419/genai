using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using jpmc_genai.Services;

namespace jpmc_genai
{
    public partial class TestCaseWindow : Window
    {
        private readonly string _testCaseId;
        private readonly string _projectId;
        private TestCase _testCase;
        private TestCaseSteps _testCaseSteps;
        private ObservableCollection<StepItem> _steps;

        public TestCaseWindow(string testCaseId, string projectId)
        {
            InitializeComponent();
            _testCaseId = testCaseId;
            _projectId = projectId;
            _steps = new ObservableCollection<StepItem>();
            StepsDataGrid.ItemsSource = _steps;
            LoadTestCaseDetails();
        }

        private async void LoadTestCaseDetails()
        {
            try
            {
                using var client = new ApiClient();
                client.SetBearer(Session.Token);

                // Fetch test case details
                var testCaseResponse = await client.GetAsync($"projects/{_projectId}/testcases");
                if (testCaseResponse.IsSuccessStatusCode)
                {
                    var testCases = JsonSerializer.Deserialize<List<TestCase>>(await testCaseResponse.Content.ReadAsStringAsync());
                    _testCase = testCases.FirstOrDefault(tc => tc.testcaseid == _testCaseId);
                    if (_testCase != null)
                    {
                        TestCaseIdTextBox.Text = _testCase.testcaseid;
                        DescriptionTextBox.Text = _testCase.testdesc;
                        PrereqTextBox.Text = _testCase.prereq;
                    }
                }

                // Fetch test case steps
                var stepsResponse = await client.GetAsync($"testcases/{_testCaseId}/steps");
                if (stepsResponse.IsSuccessStatusCode)
                {
                    _testCaseSteps = JsonSerializer.Deserialize<TestCaseSteps>(await stepsResponse.Content.ReadAsStringAsync());
                    _steps.Clear();
                    // Pair steps and args (handle unequal lengths by padding with empty strings)
                    int maxLength = Math.Max(_testCaseSteps.steps?.Count ?? 0, _testCaseSteps.args?.Count ?? 0);
                    for (int i = 0; i < maxLength; i++)
                    {
                        var step = i < (_testCaseSteps.steps?.Count ?? 0) ? _testCaseSteps.steps[i] : string.Empty;
                        var arg = i < (_testCaseSteps.args?.Count ?? 0) ? _testCaseSteps.args[i] : string.Empty;
                        _steps.Add(new StepItem { StepText = step, Argument = arg });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var client = new ApiClient();
                client.SetBearer(Session.Token);

                // Update test case details
                _testCase.testdesc = DescriptionTextBox.Text;
                _testCase.prereq = PrereqTextBox.Text;
                var testCaseContent = new StringContent(JsonSerializer.Serialize(_testCase), Encoding.UTF8, "application/json");
                var testCaseResponse = await client.PostAsync($"projects/{_projectId}/testcases", testCaseContent);

                // Update steps and args
                _testCaseSteps.steps = _steps.Select(s => s.StepText ?? string.Empty).ToList();
                _testCaseSteps.args = _steps.Select(s => s.Argument ?? string.Empty).ToList();
                _testCaseSteps.stepnum = _steps.Count;
                var stepsContent = new StringContent(JsonSerializer.Serialize(_testCaseSteps), Encoding.UTF8, "application/json");
                var stepsResponse = await client.PostAsync($"testcases/{_testCaseId}/steps", stepsContent);

                if (testCaseResponse.IsSuccessStatusCode && stepsResponse.IsSuccessStatusCode)
                {
                    MessageBox.Show("Test case updated successfully");
                    Close();
                }
                else
                {
                    MessageBox.Show("Failed to save: " + (testCaseResponse.ReasonPhrase ?? stepsResponse.ReasonPhrase));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public class StepItem
    {
        public string StepText { get; set; }
        public string Argument { get; set; }
    }
}
