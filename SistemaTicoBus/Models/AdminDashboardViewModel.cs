using TicoBus.Core.Entities;

namespace SistemaTicoBus.Web.Models
{
    public class AdminDashboardViewModel
    {
        public string NombreCompleto { get; set; } = string.Empty;
        public string Identificacion { get; set; } = string.Empty;
        public string Rol { get; set; } = "Administrador";
        public IEnumerable<Ruta> Rutas { get; set; } = new List<Ruta>();
    }
}
