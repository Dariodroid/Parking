namespace Parking.Application.UseCases;

/// <summary>Protege los importes que se guardan en columnas SQL decimal(10,2).</summary>
public static class MoneyAmount
{
    /// <summary>Mayor importe admitido por decimal(10,2).</summary>
    public const decimal Maximum = 99_999_999.99m;

    /// <summary>Comprueba que un importe no requiera redondeo ni exceda la columna SQL.</summary>
    /// <param name="amount">Valor decimal que se pretende guardar.</param>
    /// <returns>Verdadero cuando el importe tiene como máximo dos decimales y está en el rango SQL.</returns>
    public static bool IsValid(decimal amount) =>
        amount >= 0 && amount <= Maximum && amount == decimal.Truncate(amount * 100m) / 100m;

    /// <summary>Impide persistir silenciosamente fracciones de centavo o importes fuera de rango.</summary>
    /// <param name="amount">Valor que se guardará sin cambiarlo.</param>
    /// <param name="name">Nombre del importe que se mostrará al operador si es inválido.</param>
    /// <returns>El importe original, sin redondeo.</returns>
    public static decimal RequireValid(decimal amount, string name)
    {
        if (!IsValid(amount))
            throw new ArgumentOutOfRangeException(nameof(amount),
                $"{name} debe estar entre 0 y {Maximum:N2} y tener como máximo dos decimales.");
        return amount;
    }
}
