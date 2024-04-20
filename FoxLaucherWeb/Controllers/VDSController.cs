using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;

namespace FoxLaucherWeb.Controllers
{
    [ApiController]
    public class ProfileController : ControllerBase
    {
        private readonly string _profilesPath;
        private const int BufferSize = 10 * 1024 * 1024;

        public ProfileController(IConfiguration configuration)
        {
            _profilesPath = configuration.GetValue<string>("ProfilesPath");
        }

        [HttpGet("profile/{profileName}/files")]
        public IActionResult GetProfileFiles(string profileName)
        {
            try
            {
                string profilePath = Path.Combine(_profilesPath, profileName, "data");

                if (!Directory.Exists(profilePath))
                {
                    return NotFound($"Профиль {profileName} не найден");
                }

                string?[] files = Directory.GetFiles(profilePath)
                                     .Select(Path.GetFileName)
                                     .ToArray();

                return Ok(files);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Внутренняя ошибка сервера: {ex.Message}");
            }
        }

        [HttpGet("profile/{profileName}/files/mods")]
        public IActionResult GetProfileMods(string profileName)
        {
            try
            {
                string profilePath = Path.Combine(_profilesPath, profileName, "game", "mods");

                if (!Directory.Exists(profilePath))
                {
                    return NotFound($"Профиль {profileName} не найден");
                }

                string?[] files = Directory.GetFiles(profilePath)
                                     .Select(Path.GetFileName)
                                     .ToArray();

                return Ok(files);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Внутренняя ошибка сервера: {ex.Message}");
            }
        }

        [HttpGet("profile/{profileName}/hash")]
        public IActionResult GetProfileHash(string profileName)
        {
            try
            {
                string profilePath = Path.Combine(_profilesPath, profileName, "data");

                if (!Directory.Exists(profilePath))
                {
                    return NotFound($"Профиль {profileName} не найден");
                }

                string?[] requiredFiles = Directory.GetFiles(profilePath)
                                            .Select(Path.GetFileName)
                                            .ToArray();

                Dictionary<string?, string?> hashes = requiredFiles.ToDictionary(
                    file => file,
                    file =>
                    {
                        string filePath = Path.Combine(profilePath, file);
                        return System.IO.File.Exists(filePath) ? BitConverter.ToString(GetFileHash(filePath)).Replace("-", "") : null;
                    }
                );

                return Ok(hashes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Внутренняя ошибка сервера: {ex.Message}");
            }
        }

        private static byte[] GetFileHash(string filePath)
        {
            using BufferedStream stream = new(System.IO.File.OpenRead(filePath), BufferSize);
            SHA256 hashAlgorithm = SHA256.Create();
            return hashAlgorithm.ComputeHash(stream);
        }
    }
}