using TicoBus.Core.Entities;

namespace SistemaTicoBus.Web.Models
{
    public class ChoferDashboardViewModel
    {
        // Datos Personales del Chofer
        public string Identificacion { get; set; }
        public string NombreCompleto { get; set; }
        public string Rol { get; set; }

        // Lista de Viajes que tiene asignados (Simulados o de la BD)
        public List<ViajeAsignadoDTO> Viajes { get; set; }
    }

    public class ViajeAsignadoDTO
    {
        public string IdViaje { get; set; }
        public string Ruta { get; set; }
        public string UnidadPlaca { get; set; }
        public string HorarioSalida { get; set; }
        public string Ocupacion { get; set; }
        public string Estado { get; set; }
    }
}
