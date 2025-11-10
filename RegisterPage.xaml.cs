using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using jpmc_genai.Services;

namespace jpmc_genai
{
    public partial class RegisterPage : Page
    {
        public RegisterPage()
        {
            InitializeComponent();
            PasswordBox.PasswordChanged += (s, e) => PasswordPlaceholder.Visibility = string.IsNullOrEmpty(PasswordBox.Password) ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var client = new ApiClient();
                var reg = new RegisterCreate { name = NameTextBox.Text, mail = MailTextBox.Text, password = PasswordBox.Password, role = RoleTextBox.Text };
                var content = new StringContent(JsonSerializer.Serialize(reg), Encoding.UTF8, "application/json");
                var response = await client.PostAsync("User", content);
                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("User registered successfully");
                    this.NavigationService.Navigate(new LoginPage());
                }
                else
                {
                    MessageBox.Show("Registration failed: " + response.ReasonPhrase);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService.Navigate(new LoginPage());
        }
    }
}
