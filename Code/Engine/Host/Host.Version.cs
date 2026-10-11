using System;

namespace GoldSrc;

partial class Host
{
	public const int ProtocolVersion = 48;

	private const string BuildDate = "Oct  7 2024";
	private const string DefaultVersion = "1.0.1.4";
	private const string FileSystemVersion = "Stdio";
	private const string DefaultProductName = "valve";
	private const string PatchVersionKey = "PatchVersion=";
	private const string ProductNameKey = "ProductName=";
	private const int MaxVersionLength = 31;
	private const int BuildEpoch = 34995;

	private static string[] Months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
	private static int[] MonthDays = [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];

	private static string _versionString;
	private static string _productName;

	public static string ProductName()
	{
		VersionString();

		return _productName;
	}

	public static int BuildNumber()
	{
		var month = 0;
		var days = 0;

		for ( ; month < 11; month++ )
		{
			if ( string.Compare( BuildDate, 0, Months[month], 0, 3, StringComparison.OrdinalIgnoreCase ) == 0 )
				break;

			days += MonthDays[month];
		}

		days += Common.Atoi( BuildDate[4..] ) - 1;

		var year = Common.Atoi( BuildDate[7..] ) - 1900;
		var build = days - (int)((year - 1) * -365.25);

		if ( year % 4 == 0 && month > 1 )
			build++;

		return build - BuildEpoch;
	}

	public static string VersionString()
	{
		if ( _versionString is not null )
			return _versionString;

		_versionString = DefaultVersion;
		_productName = DefaultProductName;

		if ( Server.LoadMountText( "steam.inf" ) is not { } file )
			return _versionString;

		var position = 0;
		var found = 0;

		while ( found < 2 && Common.Parse( file, ref position ) is { Length: > 0 } token )
		{
			if ( token.StartsWith( PatchVersionKey, StringComparison.OrdinalIgnoreCase ) )
			{
				var version = token[PatchVersionKey.Length..];

				if ( version.Length > MaxVersionLength )
					version = version[..MaxVersionLength];

				version = $"{version}/{FileSystemVersion}";

				_versionString = version.Length > MaxVersionLength ? version[..MaxVersionLength] : version;

				found++;
			}
			else if ( token.StartsWith( ProductNameKey, StringComparison.OrdinalIgnoreCase ) )
			{
				var product = token[ProductNameKey.Length..];

				_productName = product.Length > MaxVersionLength ? product[..MaxVersionLength] : product;

				found++;
			}
		}

		return _versionString;
	}
}
