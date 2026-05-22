using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SistemaTicoBus.Web.Models;
using SistemaTicoBus.Web.Services;

namespace SistemaTicoBus.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly string _connectionString;
        private readonly IEmailService _emailService;

        public AccountController(IConfiguration configuration, IEmailService emailService)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
            _emailService = emailService;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                string query = @"
                    SELECT 
                        u.Id,
                        u.NombreUsuario,
                        u.Clave,
                        u.Correo,
                        r.Nombre AS RolNombre,
                        u.BloqueadoHasta,
                        u.IntentosFallidos
                    FROM Usuarios u
                    INNER JOIN Roles r ON u.RolId = r.Id
                    WHERE u.NombreUsuario = @NombreUsuario";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@NombreUsuario", model.Username);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            ModelState.AddModelError("", "Usuario o contraseña incorrectos.");
                            return View(model);
                        }

                        int usuarioId = Convert.ToInt32(reader["Id"]);
                        string nombreUsuario = reader["NombreUsuario"].ToString() ?? string.Empty;
                        string claveBaseDatos = reader["Clave"].ToString() ?? string.Empty;
                        string correo = reader["Correo"].ToString() ?? string.Empty;
                        string rol = reader["RolNombre"].ToString() ?? string.Empty;
                        int intentosFallidos = Convert.ToInt32(reader["IntentosFallidos"]);

                        DateTime? bloqueadoHasta = reader["BloqueadoHasta"] == DBNull.Value
                            ? null
                            : Convert.ToDateTime(reader["BloqueadoHasta"]);

                        if (bloqueadoHasta.HasValue && bloqueadoHasta.Value > DateTime.Now)
                        {
                            DateTime fechaReintento = bloqueadoHasta.Value;

                            reader.Close();

                            await EnviarCorreoCuentaBloqueadaAsync(
                                correo,
                                nombreUsuario,
                                fechaReintento
                            );

                            TimeSpan tiempoRestante = fechaReintento - DateTime.Now;

                            ModelState.AddModelError(
                                "",
                                $"Cuenta bloqueada. Intente de nuevo en {tiempoRestante.Minutes:00}:{tiempoRestante.Seconds:00}."
                            );

                            return View(model);
                        }

                        if (claveBaseDatos == model.Password)
                        {
                            reader.Close();

                            ResetearIntentos(usuarioId, connection);

                            await EnviarCorreoInicioSesionAsync(correo, nombreUsuario);

                            if (rol == "Administrador")
                            {
                                return RedirectToAction("AdminDashboard");
                            }

                            if (rol == "Chofer")
                            {
                                return RedirectToAction("ChoferDashboard");
                            }

                            if (rol == "Pasajero")
                            {
                                return RedirectToAction("PasajeroDashboard");
                            }

                            ModelState.AddModelError("", "El rol del usuario no es válido.");
                            return View(model);
                        }

                        reader.Close();

                        if (rol == "Administrador")
                        {
                            ModelState.AddModelError("", "Usuario o contraseña incorrectos.");
                            return View(model);
                        }

                        intentosFallidos++;

                        if (intentosFallidos >= 2)
                        {
                            DateTime nuevaFechaBloqueo = DateTime.Now.AddMinutes(3);

                            BloquearUsuario(usuarioId, nuevaFechaBloqueo, connection);

                            await EnviarCorreoCuentaBloqueadaAsync(
                                correo,
                                nombreUsuario,
                                nuevaFechaBloqueo
                            );

                            ModelState.AddModelError(
                                "",
                                "Demasiados intentos fallidos. Cuenta bloqueada por 3 minutos."
                            );

                            return View(model);
                        }

                        RegistrarIntentoFallido(usuarioId, intentosFallidos, connection);

                        ModelState.AddModelError("", "Usuario o contraseña incorrectos.");
                        return View(model);
                    }
                }
            }
        }

        public IActionResult Logout()
        {
            return RedirectToAction("Login", "Account");
        }

        private async Task EnviarCorreoInicioSesionAsync(string correo, string nombreUsuario)
        {
            string asunto = $"Inicio de sesión — {nombreUsuario}";

            string cuerpo =
                $"Usted inició sesión el día {DateTime.Now:dd/MM/yyyy} a las {DateTime.Now:HH:mm}.";

            await _emailService.EnviarCorreoAsync(correo, asunto, cuerpo);
        }

        private async Task EnviarCorreoCuentaBloqueadaAsync(
            string correo,
            string nombreUsuario,
            DateTime fechaReintento)
        {
            string asunto = "Cuenta bloqueada";

            string cuerpo =
                $"La cuenta {nombreUsuario} está bloqueada por 3 minutos. " +
                $"Puede reintentar el {fechaReintento:dd/MM/yyyy} a las {fechaReintento:HH:mm}.";

            await _emailService.EnviarCorreoAsync(correo, asunto, cuerpo);
        }

        private void RegistrarIntentoFallido(int usuarioId, int intentosFallidos, SqlConnection connection)
        {
            string query = @"
                UPDATE Usuarios
                SET IntentosFallidos = @IntentosFallidos
                WHERE Id = @Id";

            using (SqlCommand command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@IntentosFallidos", intentosFallidos);
                command.Parameters.AddWithValue("@Id", usuarioId);

                command.ExecuteNonQuery();
            }
        }

        private void BloquearUsuario(int usuarioId, DateTime bloqueadoHasta, SqlConnection connection)
        {
            string query = @"
                UPDATE Usuarios
                SET 
                    IntentosFallidos = 2,
                    BloqueadoHasta = @BloqueadoHasta
                WHERE Id = @Id";

            using (SqlCommand command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@BloqueadoHasta", bloqueadoHasta);
                command.Parameters.AddWithValue("@Id", usuarioId);

                command.ExecuteNonQuery();
            }
        }

        private void ResetearIntentos(int usuarioId, SqlConnection connection)
        {
            string query = @"
                UPDATE Usuarios
                SET 
                    IntentosFallidos = 0,
                    BloqueadoHasta = NULL
                WHERE Id = @Id";

            using (SqlCommand command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@Id", usuarioId);

                command.ExecuteNonQuery();
            }
        }

        public IActionResult AdminDashboard()
        {
            return RedirectToAction("Index", "Choferes");
        }

        public IActionResult ChoferDashboard()
        {
<<<<<<< Updated upstream
            var model = new ChoferDashboardViewModel
            {
                Identificacion = "1-1111-1111",
                NombreCompleto = "Mario Alfaro Rojas",
                Rol = "CHOFER AUTORIZADO",
                Viajes = new List<ViajeAsignadoDTO>
        {
            new ViajeAsignadoDTO
            {
                IdViaje = "TB-2026-0042",
                Ruta = "San José — Liberia",
                UnidadPlaca = "Mercedes-Benz (SJ-B1234)",
                HorarioSalida = "08:00 AM",
                Ocupacion = "32 / 45 Asientos",
                Estado = "EN CURSO"
            }
        }
            };
            return View(model);
=======
            return Content(
                "<h1>Ingresó como Usuario Chofer</h1><p>Panel de rutas asignadas y pasajeros.</p>",
                "text/html; charset=utf-8"
            );
>>>>>>> Stashed changes
        }

        public IActionResult PasajeroDashboard()
        {
            return Content(
                "<h1>Ingresó como Usuario Pasajero</h1><p>Panel de consultas y reservas de asientos.</p>",
                "text/html; charset=utf-8"
            );
        }
    }
}