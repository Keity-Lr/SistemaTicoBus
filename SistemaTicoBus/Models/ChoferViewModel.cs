using System.ComponentModel.DataAnnotations;

namespace SistemaTicoBus.Web.Models
{
    public class ChoferViewModel
    {
        [Required(ErrorMessage = "La identificación es requerida.")]
        [StringLength(30, ErrorMessage = "La identificación no puede superar los 30 caracteres.")]
        public string Identificacion { get; set; } = string.Empty;

        [Required(ErrorMessage = "El nombre es requerido.")]
        [StringLength(50, ErrorMessage = "El nombre no puede superar los 50 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "Los apellidos son requeridos.")]
        [StringLength(50, ErrorMessage = "Los apellidos no pueden superar los 50 caracteres.")]
        public string Apellidos { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo electrónico es requerido.")]
        [EmailAddress(ErrorMessage = "Ingrese un correo electrónico válido.")]
        [StringLength(100, ErrorMessage = "El correo no puede superar los 100 caracteres.")]
        public string Correo { get; set; } = string.Empty;

        public string? NombreUsuario { get; set; }

        public string? ClaveGenerada { get; set; }
    }
}