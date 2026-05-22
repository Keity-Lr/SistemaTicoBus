using System;
using System.Collections.Generic;
using System.Text;
using TicoBus.Core.Entities;
using System.Data.SqlClient;

namespace TicoBus.Infrastructure.Repositories
{
    public class PasajeroRepository
    {
        private readonly string _connectionString = "Server=localhost\\SQLEXPRESS;Database=TicoBusDB;Trusted_Connection=True;TrustServerCertificate=True;";

        // 1. REGISTRAR PASAJERO (Alineado con image_ebe246.png y protegido contra nulos)
        public void RegistrarPasajero(Pasajero pasajero)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // Control estricto anti-nulos para el correo electrónico
                        string correoDestino = !string.IsNullOrEmpty(pasajero.Correo)
                                                ? pasajero.Correo
                                                : $"pasajero_{pasajero.Identificacion}@ticobus.com";

                        // A. Insertar en Usuarios usando las columnas exactas observadas en SQL Server
                        string queryUsuario = @"INSERT INTO Usuarios (NombreUsuario, Clave, Correo, RolId, IntentosFallidos) 
                                                OUTPUT INSERTED.Id
                                                VALUES (@NombreUsuario, @Clave, @Correo, @RolId, @IntentosFallidos)";

                        int nuevoUsuarioId = 0;

                        using (SqlCommand cmdUser = new SqlCommand(queryUsuario, conn, transaction))
                        {
                            // Formato de nombre de usuario estándar del sistema
                            string usuarioFormateado = $"pasajero.{pasajero.Nombre.ToLower().Replace(" ", "")}";

                            cmdUser.Parameters.AddWithValue("@NombreUsuario", usuarioFormateado);
                            cmdUser.Parameters.AddWithValue("@Clave", "Pasa123*"); // Clave por defecto de tus registros
                            cmdUser.Parameters.AddWithValue("@Correo", correoDestino); // Validado sin nulos
                            cmdUser.Parameters.AddWithValue("@RolId", 3); // RolId = 3 para pasajeros
                            cmdUser.Parameters.AddWithValue("@IntentosFallidos", 0);

                            // Ejecutamos y recuperamos el Id autogenerado de Usuarios
                            nuevoUsuarioId = (int)cmdUser.ExecuteScalar();
                        }

                        // B. Insertar en Pasajeros asignando el UsuarioId obtenido
                        string queryPasajero = @"INSERT INTO Pasajeros (Identificacion, Nombre, Apellidos, UsuarioId) 
                                                 VALUES (@Id, @Nombre, @Apellidos, @UsuarioId)";

                        using (SqlCommand cmdPasajero = new SqlCommand(queryPasajero, conn, transaction))
                        {
                            cmdPasajero.Parameters.AddWithValue("@Id", pasajero.Identificacion);
                            cmdPasajero.Parameters.AddWithValue("@Nombre", pasajero.Nombre);
                            cmdPasajero.Parameters.AddWithValue("@Apellidos", pasajero.Apellidos);
                            cmdPasajero.Parameters.AddWithValue("@UsuarioId", nuevoUsuarioId);

                            cmdPasajero.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        // 2. OBTENER LISTADO (Con INNER JOIN para extraer el correo guardado en Usuarios)
        public List<Pasajero> ObtenerPasajeros(string buscarNombre = null)
        {
            var lista = new List<Pasajero>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                string query = @"SELECT p.Identificacion, p.Nombre, p.Apellidos, u.Correo 
                                 FROM Pasajeros p
                                 INNER JOIN Usuarios u ON p.UsuarioId = u.Id
                                 WHERE 1=1";

                if (!string.IsNullOrEmpty(buscarNombre))
                {
                    query += " AND p.Nombre LIKE @Buscar";
                }

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (!string.IsNullOrEmpty(buscarNombre))
                    {
                        cmd.Parameters.AddWithValue("@Buscar", "%" + buscarNombre + "%");
                    }

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lista.Add(new List<Pasajero>.Enumerator().Current ?? new Pasajero
                            {
                                Identificacion = reader["Identificacion"].ToString(),
                                Nombre = reader["Nombre"].ToString(),
                                Apellidos = reader["Apellidos"].ToString(),
                                Correo = reader["Correo"].ToString()
                            });
                        }
                    }
                }
            }
            return lista;
        }

        // 3. OBTENER POR ID
        public Pasajero ObtenerPasajeroPorId(string identificacion)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string query = @"SELECT p.Identificacion, p.Nombre, p.Apellidos, u.Correo 
                                 FROM Pasajeros p
                                 INNER JOIN Usuarios u ON p.UsuarioId = u.Id 
                                 WHERE p.Identificacion = @Id";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", identificacion);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new Pasajero
                            {
                                Identificacion = reader["Identificacion"].ToString(),
                                Nombre = reader["Nombre"].ToString(),
                                Apellidos = reader["Apellidos"].ToString(),
                                Correo = reader["Correo"].ToString()
                            };
                        }
                    }
                }
            }
            return null;
        }

        // 4. EDITAR PASAJERO
        public void EditarPasajero(Pasajero pasajero, string idOriginal)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // Modificar Correo en la tabla Usuarios
                        string queryUser = @"UPDATE Usuarios 
                                             SET Correo = @Correo 
                                             WHERE Id = (SELECT UsuarioId FROM Pasajeros WHERE Identificacion = @IdOriginal)";

                        using (SqlCommand cmdUser = new SqlCommand(queryUser, conn, transaction))
                        {
                            cmdUser.Parameters.AddWithValue("@Correo", !string.IsNullOrEmpty(pasajero.Correo) ? pasajero.Correo : "");
                            cmdUser.Parameters.AddWithValue("@IdOriginal", idOriginal);
                            cmdUser.ExecuteNonQuery();
                        }

                        // Modificar datos base en la tabla Pasajeros
                        string queryPasajero = @"UPDATE Pasajeros 
                                                 SET Identificacion = @NuevaId, Nombre = @Nombre, Apellidos = @Apellidos 
                                                 WHERE Identificacion = @IdOriginal";

                        using (SqlCommand cmdPasajero = new SqlCommand(queryPasajero, conn, transaction))
                        {
                            cmdPasajero.Parameters.AddWithValue("@NuevaId", pasajero.Identificacion);
                            cmdPasajero.Parameters.AddWithValue("@Nombre", pasajero.Nombre);
                            cmdPasajero.Parameters.AddWithValue("@Apellidos", pasajero.Apellidos);
                            cmdPasajero.Parameters.AddWithValue("@IdOriginal", idOriginal);
                            cmdPasajero.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }
    }
}
