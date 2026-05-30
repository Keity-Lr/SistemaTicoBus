using System;
using System.Collections.Generic;
using System.Text;

namespace TicoBus.Core.Entities
{
    public class Ruta
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Origen { get; set; } = string.Empty;
        public string Destino { get; set; } = string.Empty;
        public TimeSpan DuracionEstimada { get; set; }
        public decimal PrecioBase { get; set; }
    }
}
