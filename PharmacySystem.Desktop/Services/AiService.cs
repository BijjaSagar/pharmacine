using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PharmacySystem.Desktop.Services
{
    public class AiService
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;

        public AiService()
        {
            // Try to load from appsettings or environment
            _apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "";
            
            // In a real app we'd inject configuration, reading directly here for simplicity
            try {
                var json = File.ReadAllText("appsettings.json");
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("Ai", out var aiProp) && aiProp.TryGetProperty("GeminiApiKey", out var keyProp))
                {
                    var fileKey = keyProp.GetString();
                    if (!string.IsNullOrWhiteSpace(fileKey)) _apiKey = fileKey;
                }
            } catch { }

            _httpClient = new HttpClient();
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

        private async Task<string> CallGeminiAsync(string prompt, string base64Image = null, string mimeType = "image/jpeg")
        {
            if (!IsConfigured) return null;

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={_apiKey}";
            
            var parts = new List<object> { new { text = prompt } };
            
            if (!string.IsNullOrEmpty(base64Image))
            {
                parts.Add(new {
                    inline_data = new {
                        mime_type = mimeType,
                        data = base64Image
                    }
                });
            }

            var requestBody = new
            {
                contents = new[] {
                    new {
                        parts = parts
                    }
                },
                generationConfig = new
                {
                    temperature = 0.2
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);
            
            if (!response.IsSuccessStatusCode)
                throw new Exception($"AI API Error: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");

            var respJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(respJson);
            
            try {
                return doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text").GetString();
            } catch {
                return null;
            }
        }

        // 1. AI Prescription Reader (OCR + Extraction)
        public class PrescriptionItem { public string MedicineName { get; set; } public int Quantity { get; set; } }
        public async Task<List<PrescriptionItem>> ReadPrescriptionAsync(string imagePath)
        {
            byte[] imageBytes = await File.ReadAllBytesAsync(imagePath);
            string base64 = Convert.ToBase64String(imageBytes);
            string mime = Path.GetExtension(imagePath).ToLower() == ".png" ? "image/png" : "image/jpeg";

            string prompt = "You are a medical AI. Read this doctor's prescription handwriting. Extract ONLY the medicines prescribed and their intended quantity (assume standard course if vague, like 10 for 5 days bid). Return ONLY a raw JSON array format like: [{\"MedicineName\": \"Dolo 650\", \"Quantity\": 10}]. Do not include markdown code blocks.";

            var result = await CallGeminiAsync(prompt, base64, mime);
            if (string.IsNullOrWhiteSpace(result)) return new List<PrescriptionItem>();

            result = result.Replace("```json", "").Replace("```", "").Trim();
            try {
                return JsonSerializer.Deserialize<List<PrescriptionItem>>(result) ?? new List<PrescriptionItem>();
            } catch {
                return new List<PrescriptionItem>();
            }
        }

        // 2. AI Drug Interaction Checker
        public class InteractionResult { public bool HasInteraction { get; set; } public string WarningMessage { get; set; } }
        public async Task<InteractionResult> CheckInteractionsAsync(IEnumerable<string> salts)
        {
            var saltList = string.Join(", ", salts);
            string prompt = $"You are a clinical pharmacist AI. Analyze these drugs for severe drug-drug interactions: {saltList}. Return ONLY raw JSON format: {{\"HasInteraction\": true/false, \"WarningMessage\": \"short clinical warning if true, else empty\"}}. Do not include markdown.";

            var result = await CallGeminiAsync(prompt);
            if (string.IsNullOrWhiteSpace(result)) return new InteractionResult();

            result = result.Replace("```json", "").Replace("```", "").Trim();
            try {
                return JsonSerializer.Deserialize<InteractionResult>(result);
            } catch {
                return new InteractionResult();
            }
        }

        // 3. AI Predictive Restocking
        public async Task<string> PredictRestockingAsync(string salesDataJson)
        {
            string prompt = $"You are an inventory optimization AI. Analyze this recent pharmacy sales data and predict what medicines need restocking based on patterns/seasonality. Suggest a specific PO list with reasoning.\n\nData: {salesDataJson}";
            return await CallGeminiAsync(prompt);
        }

        // 4. Pharma Co-Pilot
        public async Task<string> AskCopilotAsync(string question)
        {
            string prompt = $"You are 'ClinicOS Pharma Co-Pilot', an expert clinical assistant for a pharmacy counter. Answer this question concisely and medically accurately: {question}";
            return await CallGeminiAsync(prompt);
        }

        // 5. AI Patient Churn Assistant
        public async Task<string> GenerateRetentionMessagesAsync(string customerDataJson)
        {
            string prompt = $"You are a patient retention AI. Read these chronic patients who missed their 30-day refills. Generate a friendly, empathetic WhatsApp reminder message for each, offering a 10% discount to encourage refill.\n\nData: {customerDataJson}";
            return await CallGeminiAsync(prompt);
        }
    }
}
