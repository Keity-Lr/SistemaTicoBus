using System.ComponentModel.DataAnnotations;

namespace SistemaTicoBus.Web.Models
{
    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "El nombre de usuario es requerido.")]
        [Display(Name = "Nombre")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "La clave actual es requerida.")]
        [Display(Name = "Clave actual")]
        public string ClaveActual { get; set; } = string.Empty;

        [Required(ErrorMessage = "La nueva clave es requerida.")]
        [Display(Name = "Nueva clave")]
        public string NuevaClave { get; set; } = string.Empty;
    }
}