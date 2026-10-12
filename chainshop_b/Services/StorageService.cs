using chainshop_b.Model.Dto.Response;
using System.Net.Http.Headers;

namespace chainshop_b.Services
{
    // Upload file ke Supabase Storage lewat REST API.
    // Config: Supabase:Url, Supabase:ServiceKey (rahasia), Supabase:Bucket
    public class StorageService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;

        private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB

        private static readonly Dictionary<string, string> AllowedTypes = new()
        {
            { "image/jpeg", ".jpg" },
            { "image/png", ".png" },
            { "image/webp", ".webp" },
            { "image/gif", ".gif" }
        };

        // folder yang boleh dipakai di dalam bucket
        private static readonly string[] AllowedFolders = { "products", "shops", "avatars" };

        public StorageService(HttpClient http, IConfiguration config)
        {
            _http = http;
            _config = config;
        }

        private string SupabaseUrl => (_config["Supabase:Url"] ?? "").TrimEnd('/');
        private string ServiceKey => _config["Supabase:ServiceKey"] ?? "";
        private string Bucket => string.IsNullOrWhiteSpace(_config["Supabase:Bucket"]) ? "chainshop" : _config["Supabase:Bucket"]!;

        public string GetPublicUrl(string path)
        {
            return $"{SupabaseUrl}/storage/v1/object/public/{Bucket}/{path}";
        }

        public async Task<UploadResponse> UploadImage(Guid userId, string folder, IFormFile? file)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(SupabaseUrl) || string.IsNullOrWhiteSpace(ServiceKey))
                {
                    return new UploadResponse
                    {
                        Status = false,
                        Message = "Storage is not configured (Supabase:Url / Supabase:ServiceKey)"
                    };
                }

                folder = (folder ?? "").Trim().ToLower();
                if (!AllowedFolders.Contains(folder))
                {
                    return new UploadResponse
                    {
                        Status = false,
                        Message = $"Folder must be one of: {string.Join(", ", AllowedFolders)}"
                    };
                }

                if (file == null || file.Length == 0)
                {
                    return new UploadResponse
                    {
                        Status = false,
                        Message = "Please choose a file to upload"
                    };
                }

                if (file.Length > MaxFileSize)
                {
                    return new UploadResponse
                    {
                        Status = false,
                        Message = "File size must be 5 MB or less"
                    };
                }

                if (!AllowedTypes.TryGetValue(file.ContentType.ToLower(), out var extension))
                {
                    return new UploadResponse
                    {
                        Status = false,
                        Message = "Only JPG, PNG, WebP or GIF images are allowed"
                    };
                }

                // mis. products/<userId>/<guid>.jpg
                var path = $"{folder}/{userId}/{Guid.NewGuid():N}{extension}";

                using var content = new StreamContent(file.OpenReadStream());
                content.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);

                using var request = new HttpRequestMessage(HttpMethod.Post, $"{SupabaseUrl}/storage/v1/object/{Bucket}/{path}")
                {
                    Content = content
                };
                request.Headers.Add("apikey", ServiceKey);
                // key lama (service_role) berbentuk JWT -> juga dikirim sebagai Bearer
                if (ServiceKey.StartsWith("eyJ"))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ServiceKey);
                }
                request.Headers.Add("x-upsert", "false");
                request.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromDays(365) };

                using var response = await _http.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return new UploadResponse
                    {
                        Status = false,
                        Message = $"Upload to storage failed ({(int)response.StatusCode}): {error}"
                    };
                }

                return new UploadResponse
                {
                    Status = true,
                    Message = "File uploaded successfully",
                    url = GetPublicUrl(path),
                    path = path
                };
            }
            catch (Exception ex)
            {
                return new UploadResponse
                {
                    Status = false,
                    Message = $"Server error occurred while uploading file"
                };
            }
        }
    }
}
