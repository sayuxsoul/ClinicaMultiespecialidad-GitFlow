
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

        public PacientesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Pacientes
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<object>>> ObtenerPacientes()
        {
            var pacientes = await _context.Pacientes
                .Where(p => p.Activo)
                .ToListAsync();

            return Ok(pacientes.Select(p => ConvertirADto(p)));
        }

        // GET: api/Pacientes/5
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
        [HttpGet("buscar")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<IEnumerable<object>>> BuscarPacientes(
            [FromQuery] string termino)
        {
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
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<object>> CrearPaciente(
            [FromBody] Paciente datos)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!ValidarPaciente(datos, out var error))
            {
                return BadRequest(new { mensaje = error });
            }

            var pacienteExistente = await _context.Pacientes
                .FirstOrDefaultAsync(p => p.Cedula == datos.Cedula);

            if (pacienteExistente != null)
            {
                if (pacienteExistente.Activo)
                {
                    return Conflict(new
                    {
                        mensaje = "Ya existe un paciente activo con esa cédula."
                    });
                }

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

            return CreatedAtAction(
                nameof(ObtenerPaciente),
                new { id = nuevoPaciente.PacienteId },
                ConvertirADto(nuevoPaciente));
        }

        // PUT: api/Pacientes/5
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> ActualizarPaciente(
            int id,
            [FromBody] Paciente datos)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!ValidarPaciente(datos, out var error))
            {
                return BadRequest(new { mensaje = error });
            }

            var paciente = await _context.Pacientes
                .FirstOrDefaultAsync(p => p.PacienteId == id && p.Activo);

            if (paciente == null)
            {
                return NotFound(new
                {
                    mensaje = "No se encontró el paciente."
                });
            }

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

            paciente.Activo = false;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // Validaciones del paciente
        private static bool ValidarPaciente(
            Paciente datos,
            out string error)
        {
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(datos.Cedula))
            {
                error = "La cédula es obligatoria.";
                return false;
            }

            if (datos.Cedula.Length != 11 ||
                !datos.Cedula.All(char.IsDigit))
            {
                error = "La cédula debe contener exactamente 11 dígitos.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(datos.Nombres))
            {
                error = "Los nombres son obligatorios.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(datos.Apellidos))
            {
                error = "Los apellidos son obligatorios.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(datos.Telefono))
            {
                error = "El teléfono es obligatorio.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(datos.Correo) &&
                !new System.ComponentModel.DataAnnotations.EmailAddressAttribute()
                    .IsValid(datos.Correo))
            {
                error = "El correo electrónico no tiene un formato válido.";
                return false;
            }

            if (datos.FechaNacimiento > DateOnly.FromDateTime(DateTime.Today))
            {
                error = "La fecha de nacimiento no puede ser futura.";
                return false;
            }
            {
                error = "La fecha de nacimiento no puede ser futura.";
                return false;
            }

            return true;
        }

        // Convertir paciente a DTO
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
