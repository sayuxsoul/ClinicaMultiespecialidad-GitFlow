
using ClinicaAPI.Data;
using ClinicaAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicaAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PacientesController : ControllerBase
    {
        private readonly AppDbContext _context;

        // Constructor que recibe el contexto de la base de datos.
        public PacientesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Pacientes?page=1&pageSize=10&nombre=Juan&apellido=Perez&cedula=001&correo=gmail
        // Obtiene los pacientes activos utilizando filtros y paginación.
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<object>> ObtenerPacientes(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? nombre = null,
            [FromQuery] string? apellido = null,
            [FromQuery] string? cedula = null,
            [FromQuery] string? correo = null)
        {
            // Validar los parámetros de paginación.
            if (page < 1 || pageSize < 1 || pageSize > 100)
            {
                return BadRequest(new
                {
                    mensaje = "La página debe ser mayor que cero y el tamaño debe estar entre 1 y 100."
                });
            }

            // Consultar solamente los pacientes activos.
            var consulta = _context.Pacientes
                .Where(p => p.Activo);

            // Aplicar el filtro por nombre cuando se proporcione.
            if (!string.IsNullOrWhiteSpace(nombre))
            {
                nombre = nombre.Trim();
                consulta = consulta.Where(p => p.Nombres.Contains(nombre));
            }

            // Aplicar el filtro por apellido cuando se proporcione.
            if (!string.IsNullOrWhiteSpace(apellido))
            {
                apellido = apellido.Trim();
                consulta = consulta.Where(p => p.Apellidos.Contains(apellido));
            }

            // Aplicar el filtro por cédula cuando se proporcione.
            if (!string.IsNullOrWhiteSpace(cedula))
            {
                cedula = cedula.Trim();
                consulta = consulta.Where(p => p.Cedula.Contains(cedula));
            }

            // Aplicar el filtro por correo cuando se proporcione.
            if (!string.IsNullOrWhiteSpace(correo))
            {
                correo = correo.Trim();
                consulta = consulta.Where(p => p.Correo != null &&
                                               p.Correo.Contains(correo));
            }

            // Obtener el total de registros que cumplen los filtros.
            var totalRegistros = await consulta.CountAsync();

            // Calcular el total de páginas disponibles.
            var totalPaginas = (int)Math.Ceiling(
                totalRegistros / (double)pageSize);

            // Obtener los pacientes correspondientes a la página solicitada.
            var pacientes = await consulta
                .OrderBy(p => p.PacienteId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Devolver los pacientes junto con la información de paginación.
            return Ok(new
            {
                paginaActual = page,
                tamañoPagina = pageSize,
                totalRegistros,
                totalPaginas,
                pacientes = pacientes.Select(p => ConvertirADto(p))
            });
        }

        // GET: api/Pacientes/5
        // Busca un paciente activo utilizando su identificador.
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<object>> ObtenerPaciente(int id)
        {
            var paciente = await _context.Pacientes
                .FirstOrDefaultAsync(p => p.PacienteId == id && p.Activo);

            if (paciente == null)
            {
                return NotFound(new
                {
                    mensaje = "No se encontró el paciente."
                });
            }

            return Ok(ConvertirADto(paciente));
        }

        // GET: api/Pacientes/buscar?termino=...
        // Permite buscar pacientes por nombre, apellido, cédula o código.
        [HttpGet("buscar")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<IEnumerable<object>>> BuscarPacientes(
            [FromQuery] string termino)
        {
            // Comprobar que el usuario haya escrito un término de búsqueda.
            if (string.IsNullOrWhiteSpace(termino))
            {
                return BadRequest(new
                {
                    mensaje = "Debe escribir un término para realizar la búsqueda."
                });
            }

            termino = termino.Trim();

            var pacientes = await _context.Pacientes
                .Where(p => p.Activo &&
                    (p.Nombres.Contains(termino) ||
                     p.Apellidos.Contains(termino) ||
                     p.Cedula.Contains(termino) ||
                     ("PAC-" + p.PacienteId.ToString()).Contains(termino)))
                .ToListAsync();

            return Ok(pacientes.Select(p => ConvertirADto(p)));
        }

        // POST: api/Pacientes
        // Registra un paciente nuevo o reactiva uno que estaba inactivo.
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<object>> CrearPaciente(
            [FromBody] Paciente datos)
        {
            // Comprobar que los datos recibidos sean válidos.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Aplicar las validaciones personalizadas del paciente.
            if (!ValidarPaciente(datos, out var error))
            {
                return BadRequest(new { mensaje = error });
            }

            // Comprobar si ya existe un paciente con la misma cédula.
            var pacienteExistente = await _context.Pacientes
                .FirstOrDefaultAsync(p => p.Cedula == datos.Cedula);

            if (pacienteExistente != null)
            {
                // No permitir registrar una cédula que pertenezca
                // a un paciente que todavía se encuentra activo.
                if (pacienteExistente.Activo)
                {
                    return Conflict(new
                    {
                        mensaje = "Ya existe un paciente activo con esa cédula."
                    });
                }

                // Reactivar el paciente si anteriormente fue eliminado.
                pacienteExistente.Nombres = datos.Nombres;
                pacienteExistente.Apellidos = datos.Apellidos;
                pacienteExistente.FechaNacimiento = datos.FechaNacimiento;
                pacienteExistente.Telefono = datos.Telefono;
                pacienteExistente.Correo = datos.Correo;
                pacienteExistente.Direccion = datos.Direccion;
                pacienteExistente.Activo = true;

                await _context.SaveChangesAsync();

                return Ok(ConvertirADto(pacienteExistente));
            }

            // Crear un nuevo registro con los datos recibidos.
            var nuevoPaciente = new Paciente
            {
                Cedula = datos.Cedula,
                Nombres = datos.Nombres,
                Apellidos = datos.Apellidos,
                FechaNacimiento = datos.FechaNacimiento,
                Telefono = datos.Telefono,
                Correo = datos.Correo,
                Direccion = datos.Direccion,
                FechaRegistro = DateTime.Now,
                Activo = true
            };

            _context.Pacientes.Add(nuevoPaciente);
            await _context.SaveChangesAsync();

            // Devolver la ubicación del paciente recién creado.
            return CreatedAtAction(
                nameof(ObtenerPaciente),
                new { id = nuevoPaciente.PacienteId },
                ConvertirADto(nuevoPaciente));
        }

        // PUT: api/Pacientes/5
        // Actualiza los datos de un paciente existente.
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> ActualizarPaciente(
            int id,
            [FromBody] Paciente datos)
        {
            // Comprobar que los datos recibidos sean válidos.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validar los campos del paciente antes de actualizar.
            if (!ValidarPaciente(datos, out var error))
            {
                return BadRequest(new { mensaje = error });
            }

            // Buscar el paciente que se desea modificar.
            var paciente = await _context.Pacientes
                .FirstOrDefaultAsync(p => p.PacienteId == id && p.Activo);

            if (paciente == null)
            {
                return NotFound(new
                {
                    mensaje = "No se encontró el paciente."
                });
            }

            // Comprobar que ningún otro paciente tenga esa cédula.
            var cedulaExistente = await _context.Pacientes
                .AnyAsync(p => p.Cedula == datos.Cedula &&
                               p.PacienteId != id);

            if (cedulaExistente)
            {
                return Conflict(new
                {
                    mensaje = "Ya existe otro paciente con esa cédula."
                });
            }

            // Actualizar los datos del paciente encontrado.
            paciente.Cedula = datos.Cedula;
            paciente.Nombres = datos.Nombres;
            paciente.Apellidos = datos.Apellidos;
            paciente.FechaNacimiento = datos.FechaNacimiento;
            paciente.Telefono = datos.Telefono;
            paciente.Correo = datos.Correo;
            paciente.Direccion = datos.Direccion;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // DELETE: api/Pacientes/5
        // Realiza una eliminación lógica cambiando el estado del paciente.
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> EliminarPaciente(int id)
        {
            var paciente = await _context.Pacientes
                .FirstOrDefaultAsync(p => p.PacienteId == id && p.Activo);

            if (paciente == null)
            {
                return NotFound(new
                {
                    mensaje = "No se encontró el paciente."
                });
            }

            // Marcar al paciente como inactivo sin borrar su información.
            paciente.Activo = false;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // Validaciones personalizadas de los datos del paciente.
        private static bool ValidarPaciente(
            Paciente datos,
            out string error)
        {
            error = string.Empty;

            // Comprobar que la cédula sea obligatoria.
            if (string.IsNullOrWhiteSpace(datos.Cedula))
            {
                error = "La cédula es obligatoria.";
                return false;
            }

            // Verificar que la cédula tenga exactamente 11 dígitos.
            if (datos.Cedula.Length != 11 ||
                !datos.Cedula.All(char.IsDigit))
            {
                error = "La cédula debe contener exactamente 11 dígitos.";
                return false;
            }

            // Comprobar que los nombres sean obligatorios.
            if (string.IsNullOrWhiteSpace(datos.Nombres))
            {
                error = "Los nombres son obligatorios.";
                return false;
            }

            // Comprobar que los apellidos sean obligatorios.
            if (string.IsNullOrWhiteSpace(datos.Apellidos))
            {
                error = "Los apellidos son obligatorios.";
                return false;
            }

            // El teléfono se mantiene como obligatorio según la regla actual.
            if (string.IsNullOrWhiteSpace(datos.Telefono))
            {
                error = "El teléfono es obligatorio.";
                return false;
            }

            // Validar el correo solamente cuando se proporciona.
            if (!string.IsNullOrWhiteSpace(datos.Correo) &&
                !new System.ComponentModel.DataAnnotations.EmailAddressAttribute()
                    .IsValid(datos.Correo))
            {
                error = "El correo electrónico no tiene un formato válido.";
                return false;
            }

            // Permitir una fecha de nacimiento vacía,
            // pero impedir que se registre una fecha futura.
            if (datos.FechaNacimiento.HasValue &&
                datos.FechaNacimiento.Value > DateOnly.FromDateTime(DateTime.Today))
            {
                error = "La fecha de nacimiento no puede ser futura.";
                return false;
            }

            return true;
        }

        // Convertir los datos del paciente a un objeto DTO
        // para controlar la información que devuelve la API.
        private static object ConvertirADto(Paciente paciente)
        {
            return new
            {
                pacienteId = paciente.PacienteId,
                codigoPaciente = $"PAC-{paciente.PacienteId:D4}",
                cedula = paciente.Cedula,
                nombres = paciente.Nombres,
                apellidos = paciente.Apellidos,
                fechaNacimiento = paciente.FechaNacimiento,
                telefono = paciente.Telefono,
                correo = paciente.Correo,
                direccion = paciente.Direccion,
                fechaRegistro = paciente.FechaRegistro
            };
        }
    }
}
