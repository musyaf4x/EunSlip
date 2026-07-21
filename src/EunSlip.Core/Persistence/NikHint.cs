namespace EunSlip.Core.Persistence;

public static class NikHint
{
    public static string LastFour(string nik)
    {
        if (nik.Length <= 4)
        {
            return nik;
        }

        return nik[^4..];
    }
}
