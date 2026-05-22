using System;
using System.Collections.Generic;
using System.Text;

namespace TicoBus.Core.Entities
{
    public class Pasajero
    {
        public string Identificacion { get; set; }
        public string Nombre { get; set; }
        public string Apellidos { get; set; }
        public string Correo { get; set; }
        public string Clave { get; set; } // Se generará aleatoriamente
        public string Rol { get; set; } = "Pasajero";
    }
}
