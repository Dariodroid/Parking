namespace Parking.Application.Interfaces;

/// <summary>Presenta importes con el símbolo configurado sin alterar el valor decimal.</summary>
public interface ICurrencyFormatter
{
    /// <summary>Formatea el importe para mensajes dirigidos al operador.</summary>
    /// <param name="amount">Importe exacto en moneda local.</param>
    /// <returns>Texto con símbolo y dos decimales.</returns>
    string Format(decimal amount);
}
