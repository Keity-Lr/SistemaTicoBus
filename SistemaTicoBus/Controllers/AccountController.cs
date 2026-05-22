using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SistemaTicoBus.Models;
using SistemaTicoBus.Web.Models;
using System;

namespace SistemaTicoBus.Web.Controllers
{
    public class AccountController : Controller
    {
        // Cadena de conexión directa a tu base de datos TicoBusDB
        private readonly string _connectionString = "Server=localhost\\SQLEXPRESS;Database=TicoBusDB;Trusted_Connection=True;TrustServerCertificate=True;";

        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        public IActionResult Login(LoginViewModel model)
        {
            // Validar que los campos USERNAME y PASSWORD no estén vacíos en la vista
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                // 1. Buscar si el usuario existe en la base de datos y traer su rol asignado
                string query = @"
                    SELECT u.Id, u.NombreUsuario, u.Clave, u.RolId, r.Nombre AS RolNombre, u.BloqueadoHasta, u.IntentosFallidos 
                    FROM Usuarios u
                    INNER JOIN Roles r ON u.RolId = r.Id
                    WHERE u.NombreUsuario = @Username";

                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Username", model.Username);

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        string dbPassword = reader["Clave"].ToString();
                        string rol = reader["RolNombre"].ToString();
                        int userId = Convert.ToInt32(reader["Id"]);
                        int intentosFallidos = Convert.ToInt32(reader["IntentosFallidos"]);

                        DateTime? bloqueadoHasta = reader["BloqueadoHasta"] == DBNull.Value
                            ? null
                            : Convert.ToDateTime(reader["BloqueadoHasta"]);

                        // 2. Validar si la cuenta está bloqueada temporalmente (Módulo 1)
                        if (bloqueadoHasta.HasValue && bloqueadoHasta.Value > DateTime.Now)
                        {
                            TimeSpan restante = bloqueadoHasta.Value - DateTime.Now;
                            ModelState.AddModelError("", $"Cuenta bloqueada. Intente de nuevo en {restante.Minutes:00}:{restante.Seconds:00}.");
                            return View(model);
                        }

                        // 3. Validar si la contraseña coincide
                        if (dbPassword == model.Password)
                        {
                            // Éxito: Se cierran los lectores y se restauran los intentos a 0
                            reader.Close();
                            ResetearIntentos(userId, conn);

                            // Redirigir a la vista correspondiente según el rol del usuario logueado
                            if (rol == "Administrador") return RedirectToAction("AdminDashboard");
                            if (rol == "Chofer") return RedirectToAction("ChoferDashboard");
                            if (rol == "Pasajero") return RedirectToAction("PasajeroDashboard");
                        }
                        else
                        {
                            // Falló la clave: El lector se cierra para poder actualizar los intentos en la BD
                            reader.Close();

                            // Restricción: El Administrador nunca puede quedar bloqueado bajo ninguna circunstancia
                            if (rol != "Administrador")
                            {
                                intentosFallidos++;

                                // Si el Chofer o Pasajero falla la clave 2 veces consecutivas, se bloquea por 3 minutos
                                if (intentosFallidos >= 2)
                                {
                                    BloquearUsuario(userId, conn);
                                    ModelState.AddModelError("", "Demasiados intentos fallidos. Cuenta bloqueada por 3 minutos.");
                                }
                                else
                                {
                                    RegistrarIntentoFallido(userId, intentosFallidos, conn);
                                    ModelState.AddModelError("", "Usuario o contraseña incorrectos.");
                                }
                            }
                            else
                            {
                                // Si es administrador y falla, solo muestra el error común
                                ModelState.AddModelError("", "Usuario o contraseña incorrectos.");
                            }
                            return View(model);
                        }
                    }
                    else
                    {
                        // Si el Nombre de Usuario no existe en la base de datos
                        ModelState.AddModelError("", "Usuario o contraseña incorrectos.");
                    }
                }
            }
            return View(model);
        }

        // --- MÉTODOS AUXILIARES PARA EL CONTROL DE BLOQUEOS (BASE DE DATOS) ---

        private void RegistrarIntentoFallido(int userId, int intentos, SqlConnection conn)
        {
            string q = "UPDATE Usuarios SET IntentosFallidos = @Intentos WHERE Id = @Id";
            using (SqlCommand cmd = new SqlCommand(q, conn))
            {
                cmd.Parameters.AddWithValue("@Intentos", intentos);
                cmd.Parameters.AddWithValue("@Id", userId);
                cmd.ExecuteNonQuery();
            }
        }

        private void BloquearUsuario(int userId, SqlConnection conn)
        {
            string q = "UPDATE Usuarios SET IntentosFallidos = @Intentos, BloqueadoHasta = @BloquearHasta WHERE Id = @Id";
            using (SqlCommand cmd = new SqlCommand(q, conn))
            {
                cmd.Parameters.AddWithValue("@Intentos", 2);
                cmd.Parameters.AddWithValue("@BloquearHasta", DateTime.Now.AddMinutes(3));
                cmd.Parameters.AddWithValue("@Id", userId);
                cmd.ExecuteNonQuery();
            }
        }

        private void ResetearIntentos(int userId, SqlConnection conn)
        {
            string q = "UPDATE Usuarios SET IntentosFallidos = 0, BloqueadoHasta = NULL WHERE Id = @Id";
            using (SqlCommand cmd = new SqlCommand(q, conn))
            {
                cmd.Parameters.AddWithValue("@Id", userId);
                cmd.ExecuteNonQuery();
            }
        }

        // --- PÁGINAS DE BIENVENIDA TEMPORALES SEGÚN EL ROL DE INGRESO ---

        public IActionResult AdminDashboard()
        {
            return Content("<h1>Ingresó como Administrador</h1><p>Bienvenido al panel de control total de TicoBus.</p>", "text/html; charset=utf-8");
        }

        public IActionResult ChoferDashboard()
        {
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
        }

        public IActionResult PasajeroDashboard()
        {
            return Content("<h1>Ingresó como Usuario Pasajero</h1><p>Panel de consultas y reservas de asientos.</p>", "text/html; charset=utf-8");
        }
    }
}

