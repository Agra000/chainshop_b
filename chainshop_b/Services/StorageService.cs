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
        private HttpRequestMessage CreateStorageRequest(HttpMethod method, string path)
        {
            var request = new HttpRequestMessage(method, $"{SupabaseUrl}/storage/v1/object/{Bucket}/{path}");
            request.Headers.Add("apikey", ServiceKey);
            if (ServiceKey.StartsWith("eyJ"))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ServiceKey);
            }
            return request;
        }

        // URL publik / path -> path di dalam bucket. null jika bukan file dari bucket ini.
        public string? GetPathFromUrl(string? urlOrPath)
        {
            if (string.IsNullOrWhiteSpace(urlOrPath)) return null;

            var prefix = $"{SupabaseUrl}/storage/v1/object/public/{Bucket}/";
            var path = urlOrPath.StartsWith(prefix) ? urlOrPath[prefix.Length..] : urlOrPath;

            // format wajib: <folder>/<userId>/<file>, tanpa ".." dan bukan URL lain (mis. base64 / gambar lama)
            var parts = path.Split('/');
            if (parts.Length != 3 || !AllowedFolders.Contains(parts[0]) || !Guid.TryParse(parts[1], out _) ||
                path.Contains("..") || path.Contains(':'))
            {
                return null;
            }

            return path;
        }

        // Delete: hapus file milik user (path harus di folder <folder>/<userId>/...)
        public async Task<UploadResponse> DeleteImage(Guid userId, string? urlOrPath)
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

                var path = GetPathFromUrl(urlOrPath);
                if (path == null)
                {
                    return new UploadResponse
                    {
                        Status = false,
                        Message = "Image is not stored in this storage"
                    };
                }

                if (path.Split('/')[1] != userId.ToString())
                {
                    return new UploadResponse
                    {
                        Status = false,
                        Message = "You are not allowed to delete this image"
                    };
                }

                using var request = CreateStorageRequest(HttpMethod.Delete, path);
                using var response = await _http.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return new UploadResponse
                    {
                        Status = false,
                        Message = "You are not allowed to delete this image"
                    };
                }

                return new UploadResponse
                {
                    Status = true,
                    Message = "Image deleted successfully",
                    path = path
                };
            }
            catch (Exception ex)
            {
                return new UploadResponse
                {
                    Status = false,
                    Message = $"Delete from Supabase Storage error"
                };
            }
        }

        // Update: upload file baru, lalu hapus file lama (jika file lama milik user yang sama)
        public async Task<UploadResponse> ReplaceImage(Guid userId, string folder, IFormFile? file, string? oldUrlOrPath)
        {
            var upload = await UploadImage(userId, folder, file);
            if (!upload.Status) return upload;

            if (GetPathFromUrl(oldUrlOrPath) != null)
            {
                var deleted = await DeleteImage(userId, oldUrlOrPath);
                if (!deleted.Status)
                {
                    return new UploadResponse
                    {
                        Status = false,
                        Message = "Old Image was not deleted"
                    };
                }
            }

            upload.Message = "Image replaced successfully";
            return upload;
        }

        // dipakai service lain (hapus produk / toko / ganti gambar): gagal hapus tidak menggagalkan proses utama
        public async Task DeleteImageQuietly(Guid ownerUserId, string? urlOrPath)
        {
            if (GetPathFromUrl(urlOrPath) == null) return;
            var res = await DeleteImage(ownerUserId, urlOrPath);
        }
    }
}
