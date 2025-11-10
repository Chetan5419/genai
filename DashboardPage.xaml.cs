using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using jpmc_genai.Services;

namespace jpmc_genai
{
    public partial class DashboardPage : Page
    {
        private readonly string _projectId;
        private List<TestCase> _allTestCases;

        public DashboardPage(string projectId)
        {
            InitializeComponent();
            _projectId = projectId;
            var project = Session.CurrentUser?.projects?.FirstOrDefault(p => p.projectid == projectId);
            ProjectTitleTextBlock.Text = project?.title ?? "Project";
            ProjectDetailsTextBlock.Text = $"ID: {projectId}\nType: {project?.projecttype}\nStarted: {project?.startdate}";
            LoadTestCases();
        }

        private async void LoadTestCases()
        {
            try
            {
                using var client = new ApiClient();
                client.SetBearer(Session.Token);
                var response = await client.GetAsync($"projects/{_projectId}/testcases");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    _allTestCases = JsonSerializer.Deserialize<List<TestCase>>(json);
                    UpdateTestCasesDisplay(_allTestCases);
                }
                else
                {
                    MessageBox.Show("Failed to load test cases: " + response.ReasonPhrase);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }

        private void UpdateTestCasesDisplay(List<TestCase> testCases)
        {
            DataContext = new { TestCases = testCases };
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_allTestCases == null) return;
            var searchText = SearchTextBox.Text.ToLower();
            var filtered = string.IsNullOrEmpty(searchText)
                ? _allTestCases
                : _allTestCases.Where(tc => tc.testcaseid.ToLower().Contains(searchText)).ToList();
            UpdateTestCasesDisplay(filtered);
        }

        private void TestCaseCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string testCaseId)
            {
                var window = new TestCaseWindow(testCaseId, _projectId);
                window.ShowDialog();
            }
        }

        private void BackToProjects_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new ProjectPage());
        }

        private void AITestExecutor_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AITestExecutorPage(_projectId));
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
    }
}
