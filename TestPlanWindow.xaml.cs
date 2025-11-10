using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Text.Json;
using System.IO;

namespace jpmc_genai
{
    public partial class TestPlanWindow : Window
    {
        private readonly TestPlan _testPlan;
        private string _tempJsonPath;

        public TestPlanWindow(TestPlan testPlan)
        {
            InitializeComponent();
            _testPlan = testPlan;
            _tempJsonPath = Path.Combine(Path.GetTempPath(), $"{testPlan.current_testid}_testplan.json");
            LoadTestPlan();
        }

        private void LoadTestPlan()
        {
            CurrentTestIdTextBox.Text = _testPlan.current_testid;
            // Load pretest steps
            var pretestSteps = _testPlan.pretestid_steps?.SelectMany(kv => kv.Value.Select(step => new PretestStepItem
            {
                PretestId = kv.Key,
                Step = step.Key,
                TestData = step.Value
            })).ToList() ?? new List<PretestStepItem>();
            PretestStepsDataGrid.ItemsSource = pretestSteps;
            
            // Load pretest scripts
            var pretestScripts = (_testPlan.pretestid_scripts ?? new Dictionary<string, string>())
                .Select(kv => new PretestScriptItem
                {
                    PretestId = kv.Key,
                    Script = kv.Value
                }).ToList();
            PretestScriptsDataGrid.ItemsSource = pretestScripts;
            
            // Load current BDD steps
            var currentBddSteps = _testPlan.current_bdd_steps?.Select(kv => new BddStepItem
            {
                Step = kv.Key,
                TestData = kv.Value
            }).ToList() ?? new List<BddStepItem>();
            CurrentBddStepsDataGrid.ItemsSource = currentBddSteps;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Update test plan
                _testPlan.pretestid_steps = PretestStepsDataGrid.Items.Cast<PretestStepItem>()
                    .GroupBy(item => item.PretestId)
                    .ToDictionary(g => g.Key, g => g.ToDictionary(item => item.Step, item => item.TestData));
                    
                _testPlan.pretestid_scripts = PretestScriptsDataGrid.Items.Cast<PretestScriptItem>()
                    .ToDictionary(item => item.PretestId, item => item.Script);
                    
                _testPlan.current_bdd_steps = CurrentBddStepsDataGrid.Items.Cast<BddStepItem>()
                    .ToDictionary(item => item.Step, item => item.TestData);
                
                File.WriteAllText(_tempJsonPath, JsonSerializer.Serialize(_testPlan, new JsonSerializerOptions { WriteIndented = true }));
                MessageBox.Show("Test plan saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving test plan: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public class PretestStepItem
    {
        public string PretestId { get; set; }
        public string Step { get; set; }
        public string TestData { get; set; }
    }

    public class PretestScriptItem
    {
        public string PretestId { get; set; }
        public string Script { get; set; }
    }

    public class BddStepItem
    {
        public string Step { get; set; }
        public string TestData { get; set; }
    }
}
