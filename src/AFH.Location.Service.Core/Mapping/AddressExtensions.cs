using AFH.Location.Service.Core.Contracts.V1.Requests;
using System.Text;

namespace AFH.Location.Service.Core.Common.Extensions;

public static class AddressExtensions
{
    public static string ToSingleLine(this Address address)
    {
        if (address is null)
            throw new ArgumentNullException(nameof(address));

        var sb = new StringBuilder();

        sb.Append(address.Line1);

        if (!string.IsNullOrWhiteSpace(address.Line2))
            sb.Append(", ").Append(address.Line2);

        sb.Append(", ").Append(address.Town);

        if (!string.IsNullOrWhiteSpace(address.Postcode))
            sb.Append(", ").Append(address.Postcode);

        if (!string.IsNullOrWhiteSpace(address.Country))
            sb.Append(", ").Append(address.Country);

        return sb.ToString();
    }
}