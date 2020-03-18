using System.ComponentModel.DataAnnotations;

namespace Helium.CI.Server.UI
{
    public sealed class ProjectEditModel
    {
        [Required]
        public string? Name { get; set; }

        [Required]
        public string? Url { get; set; }

        [Required]
        public string? Branch { get; set; }
        
        [Required]
        public string? Path { get; set; }
        
    }
}