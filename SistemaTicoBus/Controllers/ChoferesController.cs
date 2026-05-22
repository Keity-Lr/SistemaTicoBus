using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SistemaTicoBus.Web.Models;
using SistemaTicoBus.Web.Services;
using System.Data;
using System.Text;

namespace SistemaTicoBus.Web.Controllers
{
    public class ChoferesController : Controller
    {
        private const string RolAdministrador = "Administrador";

        private readonly string _connectionString;
        private readonly IEmailService _emailService;

        public ChoferesController(IConfiguration configuration, IEmailService emailService)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
            _emailService = emailService;
        }

        [HttpGet]
        public IActionResult Index(string? busqueda)
        {
            if (!UsuarioEsAdministrador())
            {
                return RedirectToAction("Login", "Account");
            }

            List<ChoferViewModel> choferes = ObtenerChoferes(busqueda);

            ViewBag.Busqueda = busqueda;

            return View(choferes);
        }

        [HttpGet]
        public IActionResult Create()
        {
            if (!UsuarioEsAdministrador())
            {
                return RedirectToAction("Login", "Account");
            }

            return View(new ChoferViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ChoferViewModel model)
        {
            if (!UsuarioEsAdministrador())
            {
                return RedirectToAction("Login", "Account");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (ExisteChofer(model.Identificacion))
            {
                ModelState.AddModelError("Identificacion", "Ya existe un chofer con esa identificación.");
                return View(model);
            }

            if (ExisteCorreo(model.Correo))
            {
                ModelState.AddModelError("Correo", "Ya existe un usuario registrado con ese correo.");
                return View(model);
            }

            string nombreUsuario = GenerarNombreUsuario(model.Nombre, model.Apellidos);
            string claveGenerada = GenerarClaveAleatoria();

            try
            {
                CrearChoferConUsuario(model, nombreUsuario, claveGenerada);

                await EnviarClaveChoferAsync(model.Correo, nombreUsuario, claveGenerada);

                TempData["MensajeExito"] =
                    $"Chofer registrado correctamente. Usuario generado: {nombreUsuario}. La clave temporal fue enviada al correo indicado.";

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                ModelState.AddModelError("", "Ocurrió un error al registrar el chofer.");
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult Edit(string id)
        {
            if (!UsuarioEsAdministrador())
            {
                return RedirectToAction("Login", "Account");
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                return RedirectToAction(nameof(Index));
            }

            ChoferViewModel? chofer = ObtenerChoferPorIdentificacion(id);

            if (chofer == null)
            {
                return RedirectToAction(nameof(Index));
            }

            return View(chofer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(string id, ChoferViewModel model)
        {
            if (!UsuarioEsAdministrador())
            {
                return RedirectToAction("Login", "Account");
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                return RedirectToAction(nameof(Index));
            }

            ModelState.Remove(nameof(ChoferViewModel.Correo));

            if (!ModelState.IsValid)
            {
                ChoferViewModel? choferActual = ObtenerChoferPorIdentificacion(id);

                if (choferActual != null)
                {
                    model.Correo = choferActual.Correo;
                    model.NombreUsuario = choferActual.NombreUsuario;
                }

                return View(model);
            }

            if (ExisteOtraIdentificacion(id, model.Identificacion))
            {
                ModelState.AddModelError("Identificacion", "Ya existe otro chofer con esa identificación.");
                return View(model);
            }

            ActualizarChofer(id, model);

            TempData["MensajeExito"] = "Chofer actualizado correctamente.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(string id)
        {
            if (!UsuarioEsAdministrador())
            {
                return RedirectToAction("Login", "Account");
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                return RedirectToAction(nameof(Index));
            }

            if (ChoferTieneViajes(id))
            {
                TempData["MensajeError"] = "No se puede eliminar el chofer porque tiene viajes registrados.";
                return RedirectToAction(nameof(Index));
            }

            EliminarChoferYUsuario(id);

            TempData["MensajeExito"] = "Chofer eliminado correctamente.";

            return RedirectToAction(nameof(Index));
        }

        private bool UsuarioEsAdministrador()
        {
            string? rol = HttpContext.Session.GetString("Rol");

            return rol == RolAdministrador;
        }

        private List<ChoferViewModel> ObtenerChoferes(string? busqueda)
        {
            List<ChoferViewModel> choferes = new List<ChoferViewModel>();

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                string query = @"
                    SELECT 
                        c.Identificacion,
                        c.Nombre,
                        c.Apellidos,
                        u.Correo,
                        u.NombreUsuario
                    FROM Choferes c
                    INNER JOIN Usuarios u ON c.UsuarioId = u.Id
                    WHERE 
                        @Busqueda IS NULL
                        OR @Busqueda = ''
                        OR c.Nombre LIKE '%' + @Busqueda + '%'
                        OR c.Apellidos LIKE '%' + @Busqueda + '%'
                    ORDER BY c.Nombre, c.Apellidos";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.Add("@Busqueda", SqlDbType.VarChar, 100).Value =
                        string.IsNullOrWhiteSpace(busqueda) ? DBNull.Value : busqueda.Trim();

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            choferes.Add(new ChoferViewModel
                            {
                                Identificacion = reader["Identificacion"].ToString() ?? string.Empty,
                                Nombre = reader["Nombre"].ToString() ?? string.Empty,
                                Apellidos = reader["Apellidos"].ToString() ?? string.Empty,
                                Correo = reader["Correo"].ToString() ?? string.Empty,
                                NombreUsuario = reader["NombreUsuario"].ToString() ?? string.Empty
                            });
                        }
                    }
                }
            }

            return choferes;
        }

        private void CrearChoferConUsuario(
            ChoferViewModel model,
            string nombreUsuario,
            string claveGenerada)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        int rolChoferId = ObtenerRolChoferId(connection, transaction);

                        int usuarioId = CrearUsuarioChofer(
                            connection,
                            transaction,
                            nombreUsuario,
                            claveGenerada,
                            model.Correo,
                            rolChoferId
                        );

                        CrearChofer(connection, transaction, model, usuarioId);

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private void ActualizarChofer(string identificacionActual, ChoferViewModel model)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                string query = @"
                    UPDATE Choferes
                    SET 
                        Identificacion = @NuevaIdentificacion,
                        Nombre = @Nombre,
                        Apellidos = @Apellidos
                    WHERE Identificacion = @IdentificacionActual";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.Add("@NuevaIdentificacion", SqlDbType.VarChar, 30).Value = model.Identificacion.Trim();
                    command.Parameters.Add("@Nombre", SqlDbType.VarChar, 50).Value = model.Nombre.Trim();
                    command.Parameters.Add("@Apellidos", SqlDbType.VarChar, 50).Value = model.Apellidos.Trim();
                    command.Parameters.Add("@IdentificacionActual", SqlDbType.VarChar, 30).Value = identificacionActual.Trim();

                    command.ExecuteNonQuery();
                }
            }
        }

        private void EliminarChoferYUsuario(string identificacion)
        {
            int usuarioId = ObtenerUsuarioIdDeChofer(identificacion);

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        string deleteChoferQuery = "DELETE FROM Choferes WHERE Identificacion = @Identificacion";

                        using (SqlCommand command = new SqlCommand(deleteChoferQuery, connection, transaction))
                        {
                            command.Parameters.Add("@Identificacion", SqlDbType.VarChar, 30).Value = identificacion.Trim();
                            command.ExecuteNonQuery();
                        }

                        string deleteUsuarioQuery = "DELETE FROM Usuarios WHERE Id = @UsuarioId";

                        using (SqlCommand command = new SqlCommand(deleteUsuarioQuery, connection, transaction))
                        {
                            command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = usuarioId;
                            command.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private async Task EnviarClaveChoferAsync(string correo, string nombreUsuario, string claveGenerada)
        {
            string asunto = "Usuario Chofer creado — TicoBus";

            string cuerpo =
                $"Se creó su usuario de Chofer en TicoBus.\n\n" +
                $"Nombre de usuario: {nombreUsuario}\n" +
                $"Clave temporal: {claveGenerada}\n\n" +
                $"Por seguridad, cambie su clave al ingresar al sistema.";

            await _emailService.EnviarCorreoAsync(correo, asunto, cuerpo);
        }

        private bool ExisteChofer(string identificacion)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                string query = "SELECT COUNT(*) FROM Choferes WHERE Identificacion = @Identificacion";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.Add("@Identificacion", SqlDbType.VarChar, 30).Value = identificacion.Trim();

                    int total = Convert.ToInt32(command.ExecuteScalar());

                    return total > 0;
                }
            }
        }

        private bool ExisteCorreo(string correo)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                string query = "SELECT COUNT(*) FROM Usuarios WHERE Correo = @Correo";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.Add("@Correo", SqlDbType.VarChar, 100).Value = correo.Trim();

                    int total = Convert.ToInt32(command.ExecuteScalar());

                    return total > 0;
                }
            }
        }

        private bool ExisteNombreUsuario(string nombreUsuario)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                string query = "SELECT COUNT(*) FROM Usuarios WHERE NombreUsuario = @NombreUsuario";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.Add("@NombreUsuario", SqlDbType.VarChar, 50).Value = nombreUsuario;

                    int total = Convert.ToInt32(command.ExecuteScalar());

                    return total > 0;
                }
            }
        }

        private bool ExisteOtraIdentificacion(string identificacionActual, string nuevaIdentificacion)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                string query = @"
                    SELECT COUNT(*) 
                    FROM Choferes 
                    WHERE Identificacion = @NuevaIdentificacion
                    AND Identificacion <> @IdentificacionActual";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.Add("@NuevaIdentificacion", SqlDbType.VarChar, 30).Value = nuevaIdentificacion.Trim();
                    command.Parameters.Add("@IdentificacionActual", SqlDbType.VarChar, 30).Value = identificacionActual.Trim();

                    int total = Convert.ToInt32(command.ExecuteScalar());

                    return total > 0;
                }
            }
        }

        private bool ChoferTieneViajes(string identificacion)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                string query = "SELECT COUNT(*) FROM Viajes WHERE ChoferId = @ChoferId";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.Add("@ChoferId", SqlDbType.VarChar, 30).Value = identificacion.Trim();

                    int total = Convert.ToInt32(command.ExecuteScalar());

                    return total > 0;
                }
            }
        }

        private ChoferViewModel? ObtenerChoferPorIdentificacion(string identificacion)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                string query = @"
                    SELECT 
                        c.Identificacion,
                        c.Nombre,
                        c.Apellidos,
                        u.Correo,
                        u.NombreUsuario
                    FROM Choferes c
                    INNER JOIN Usuarios u ON c.UsuarioId = u.Id
                    WHERE c.Identificacion = @Identificacion";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.Add("@Identificacion", SqlDbType.VarChar, 30).Value = identificacion.Trim();

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return null;
                        }

                        return new ChoferViewModel
                        {
                            Identificacion = reader["Identificacion"].ToString() ?? string.Empty,
                            Nombre = reader["Nombre"].ToString() ?? string.Empty,
                            Apellidos = reader["Apellidos"].ToString() ?? string.Empty,
                            Correo = reader["Correo"].ToString() ?? string.Empty,
                            NombreUsuario = reader["NombreUsuario"].ToString() ?? string.Empty
                        };
                    }
                }
            }
        }

        private int ObtenerRolChoferId(SqlConnection connection, SqlTransaction transaction)
        {
            string query = "SELECT Id FROM Roles WHERE Nombre = 'Chofer'";

            using (SqlCommand command = new SqlCommand(query, connection, transaction))
            {
                object? result = command.ExecuteScalar();

                if (result == null)
                {
                    throw new InvalidOperationException("No existe el rol Chofer en la base de datos.");
                }

                return Convert.ToInt32(result);
            }
        }

        private int CrearUsuarioChofer(
            SqlConnection connection,
            SqlTransaction transaction,
            string nombreUsuario,
            string claveGenerada,
            string correo,
            int rolChoferId)
        {
            string query = @"
                INSERT INTO Usuarios 
                    (NombreUsuario, Clave, Correo, RolId, BloqueadoHasta, IntentosFallidos)
                OUTPUT INSERTED.Id
                VALUES 
                    (@NombreUsuario, @Clave, @Correo, @RolId, NULL, 0)";

            using (SqlCommand command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.Add("@NombreUsuario", SqlDbType.VarChar, 50).Value = nombreUsuario;
                command.Parameters.Add("@Clave", SqlDbType.VarChar, 255).Value = claveGenerada;
                command.Parameters.Add("@Correo", SqlDbType.VarChar, 100).Value = correo.Trim();
                command.Parameters.Add("@RolId", SqlDbType.Int).Value = rolChoferId;

                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        private void CrearChofer(
            SqlConnection connection,
            SqlTransaction transaction,
            ChoferViewModel model,
            int usuarioId)
        {
            string query = @"
                INSERT INTO Choferes
                    (Identificacion, Nombre, Apellidos, UsuarioId)
                VALUES
                    (@Identificacion, @Nombre, @Apellidos, @UsuarioId)";

            using (SqlCommand command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.Add("@Identificacion", SqlDbType.VarChar, 30).Value = model.Identificacion.Trim();
                command.Parameters.Add("@Nombre", SqlDbType.VarChar, 50).Value = model.Nombre.Trim();
                command.Parameters.Add("@Apellidos", SqlDbType.VarChar, 50).Value = model.Apellidos.Trim();
                command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = usuarioId;

                command.ExecuteNonQuery();
            }
        }

        private int ObtenerUsuarioIdDeChofer(string identificacion)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                string query = "SELECT UsuarioId FROM Choferes WHERE Identificacion = @Identificacion";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.Add("@Identificacion", SqlDbType.VarChar, 30).Value = identificacion.Trim();

                    object? result = command.ExecuteScalar();

                    if (result == null)
                    {
                        return 0;
                    }

                    return Convert.ToInt32(result);
                }
            }
        }

        private string GenerarNombreUsuario(string nombre, string apellidos)
        {
            string primerNombre = ObtenerPrimeraPalabra(nombre);
            string primerApellido = ObtenerPrimeraPalabra(apellidos);

            string nombreBase = $"chofer.{primerNombre}.{primerApellido}".ToLower();
            nombreBase = LimpiarTextoUsuario(nombreBase);

            string nombreUsuario = nombreBase;
            int contador = 1;

            while (ExisteNombreUsuario(nombreUsuario))
            {
                nombreUsuario = $"{nombreBase}{contador}";
                contador++;
            }

            return nombreUsuario;
        }

        private string GenerarClaveAleatoria()
        {
            string caracteres = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
            Random random = new Random();
            StringBuilder clave = new StringBuilder();

            for (int i = 0; i < 8; i++)
            {
                int posicion = random.Next(caracteres.Length);
                clave.Append(caracteres[posicion]);
            }

            clave.Append("*1");

            return clave.ToString();
        }

        private string ObtenerPrimeraPalabra(string texto)
        {
            string[] partes = texto.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (partes.Length == 0)
            {
                return "usuario";
            }

            return partes[0];
        }

        private string LimpiarTextoUsuario(string texto)
        {
            return texto
                .Replace("á", "a")
                .Replace("é", "e")
                .Replace("í", "i")
                .Replace("ó", "o")
                .Replace("ú", "u")
                .Replace("ñ", "n")
                .Replace("Á", "a")
                .Replace("É", "e")
                .Replace("Í", "i")
                .Replace("Ó", "o")
                .Replace("Ú", "u")
                .Replace("Ñ", "n");
        }
    }
}