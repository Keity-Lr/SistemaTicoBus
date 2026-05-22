using Microsoft.AspNetCore.Mvc;
using TicoBus.Core.Entities;
using TicoBus.Infrastructure.Repositories;

namespace SistemaTicoBus.Web.Controllers
{
    public class PasajerosController : Controller
    {
        private readonly PasajeroRepository _repository = new PasajeroRepository();

        // GET: /Pasajeros/ListadoPasajeros
        public IActionResult ListadoPasajeros(string buscarNombre, string identificacionEditar)
        {
            // 1. Filtrado exclusivo por Nombre directo en la BD
            var pasajeros = _repository.ObtenerPasajeros(buscarNombre);
            ViewBag.Busqueda = buscarNombre;

            // 2. Si se presionó Editar, cargamos el pasajero para el formulario
            if (!string.IsNullOrEmpty(identificacionEditar))
            {
                var pasajero = _repository.ObtenerPasajeroPorId(identificacionEditar);
                ViewBag.PasajeroEditar = pasajero;
                ViewBag.IdOriginal = identificacionEditar; // Guardamos la cédula original antes de ser editada
            }

            return View(pasajeros);
        }

        // POST: /Pasajeros/RegistrarPasajeroGuardar
        [HttpPost]
        public IActionResult RegistrarPasajeroGuardar(Pasajero model)
        {
            // CORREGIDO: Ahora se valida estrictamente que el Correo NO venga vacío para cumplir con la rúbrica
            if (!string.IsNullOrEmpty(model.Identificacion) &&
                !string.IsNullOrEmpty(model.Nombre) &&
                !string.IsNullOrEmpty(model.Apellidos) &&
                !string.IsNullOrEmpty(model.Correo))
            {
                // Mantenemos la clave por defecto que usa tu base de datos para evitar conflictos de logueo
                model.Clave = "Pasa123*";
                model.Rol = "Pasajero";

                // Se envía el modelo completo con el correo ingresado por el usuario
                _repository.RegistrarPasajero(model);

                TempData["CorreoSimulado"] = $"Pasajero registrado con éxito. Cuenta de acceso creada para el correo: {model.Correo}";
            }
            else
            {
                // Alerta por si falta rellenar algún espacio obligatorio
                TempData["CorreoSimulado"] = "⚠️ Error: Todos los campos son requeridos (Identificación, Nombre, Apellidos y Correo).";
            }

            return RedirectToAction("ListadoPasajeros");
        }

        // POST: /Pasajeros/EditarPasajeroGuardar
        [HttpPost]
        public IActionResult EditarPasajeroGuardar(Pasajero model, string idOriginal)
        {
            // CORREGIDO: Se añade el Correo a las validaciones de edición
            if (!string.IsNullOrEmpty(model.Identificacion) &&
                !string.IsNullOrEmpty(model.Nombre) &&
                !string.IsNullOrEmpty(model.Apellidos) &&
                !string.IsNullOrEmpty(model.Correo) &&
                !string.IsNullOrEmpty(idOriginal))
            {
                // Pasamos el modelo con el correo actualizado y la cédula original para aplicar los cambios en cascada
                _repository.EditarPasajero(model, idOriginal);

                TempData["CorreoSimulado"] = $"Los datos y correo del pasajero fueron actualizados con éxito.";
            }
            else
            {
                TempData["CorreoSimulado"] = "⚠️ Error: No se pudieron guardar los cambios. Verifique que no queden datos vacíos.";
            }

            return RedirectToAction("ListadoPasajeros");
        }
    }
}
