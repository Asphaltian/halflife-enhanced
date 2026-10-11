using System;
using System.Text;

namespace GoldSrc;

partial class Client
{
	public const string DefaultPlayerName = "unnamed";

	private static string _playerName = DefaultPlayerName;
	private static string _password = "";
	private static string _team = "";
	private static string _skin = "";
	private static string _model = "";
	private static string _topColor = "0";
	private static string _bottomColor = "0";
	private static string _rate = "30000";
	private static string _updateRate = "60";
	private static string _localWeapons = "1";
	private static string _lagCompensation = "1";
	private static string _downloadMax = "512";

	[ConVar( "name", ConVarFlags.Saved | ConVarFlags.UserInfo )]
	public static string PlayerName
	{
		get => _playerName;
		set => SetUserInfo( "name", ref _playerName, StripWhitespace( Printable( value ) ) );
	}

	[ConVar( "password", ConVarFlags.UserInfo )]
	private static string Password
	{
		get => _password;
		set => SetUserInfo( "password", ref _password, value );
	}

	[ConVar( "team", ConVarFlags.Saved | ConVarFlags.UserInfo )]
	private static string Team
	{
		get => _team;
		set => SetUserInfo( "team", ref _team, value );
	}

	[ConVar( "skin", ConVarFlags.Saved | ConVarFlags.UserInfo )]
	private static string Skin
	{
		get => _skin;
		set => SetUserInfo( "skin", ref _skin, value );
	}

	[ConVar( "model", ConVarFlags.Saved | ConVarFlags.UserInfo )]
	private static string Model
	{
		get => _model;
		set => SetUserInfo( "model", ref _model, value );
	}

	[ConVar( "topcolor", ConVarFlags.Saved | ConVarFlags.UserInfo )]
	private static string TopColor
	{
		get => _topColor;
		set => SetUserInfo( "topcolor", ref _topColor, value );
	}

	[ConVar( "bottomcolor", ConVarFlags.Saved | ConVarFlags.UserInfo )]
	private static string BottomColor
	{
		get => _bottomColor;
		set => SetUserInfo( "bottomcolor", ref _bottomColor, value );
	}

	[ConVar( "rate", ConVarFlags.Saved | ConVarFlags.UserInfo )]
	private static string Rate
	{
		get => _rate;
		set => SetUserInfo( "rate", ref _rate, value );
	}

	[ConVar( "cl_updaterate", ConVarFlags.Saved | ConVarFlags.UserInfo )]
	private static string UpdateRate
	{
		get => _updateRate;
		set => SetUserInfo( "cl_updaterate", ref _updateRate, value );
	}

	[ConVar( "cl_lw", ConVarFlags.Saved | ConVarFlags.UserInfo )]
	private static string LocalWeapons
	{
		get => _localWeapons;
		set => SetUserInfo( "cl_lw", ref _localWeapons, value );
	}

	[ConVar( "cl_lc", ConVarFlags.Saved | ConVarFlags.UserInfo )]
	private static string LagCompensation
	{
		get => _lagCompensation;
		set => SetUserInfo( "cl_lc", ref _lagCompensation, value );
	}

	[ConVar( "cl_dlmax", ConVarFlags.Saved | ConVarFlags.UserInfo )]
	private static string DownloadMax
	{
		get => _downloadMax;
		set => SetUserInfo( "cl_dlmax", ref _downloadMax, value );
	}

	[ConVar( "cl_himodels", ConVarFlags.Saved )]
	public static float HighModels { get; set; }

	[ConVar( "cl_logofile", ConVarFlags.Saved )]
	public static string LogoFile { get; set; } = "lambda";

	[ConVar( "cl_logocolor", ConVarFlags.Saved )]
	public static string LogoColor { get; set; } = "orange";

	public static IEnumerable<(string Key, string Value)> UserInfo()
	{
		yield return ("name", _playerName);
		yield return ("password", _password);
		yield return ("team", _team);
		yield return ("skin", _skin);
		yield return ("model", _model);
		yield return ("topcolor", _topColor);
		yield return ("bottomcolor", _bottomColor);
		yield return ("rate", _rate);
		yield return ("cl_updaterate", _updateRate);
		yield return ("cl_lw", _localWeapons);
		yield return ("cl_lc", _lagCompensation);
		yield return ("cl_dlmax", _downloadMax);
	}

	private static string _userInfo;

	public static string UserInfoString()
	{
		if ( _userInfo is not null )
			return _userInfo;

		_userInfo = "";

		foreach ( var (key, value) in UserInfo() )
			Info.SetValueForKey( ref _userInfo, key, value, PlayerInfo.MaxInfoString );

		return _userInfo;
	}

	public static void SetUserInfoKey( string key, string value )
	{
		UserInfoString();

		Info.SetValueForKey( ref _userInfo, key, value, PlayerInfo.MaxInfoString );
	}

	public static void SetUserInfo( string key, ref string field, string value )
	{
		var changed = field != value;

		field = value;

		SetUserInfoKey( key, value );

		if ( !changed || Current is not { } client || client._state < ClientState.Connected )
			return;

		client._netchan.Message.WriteByte( Server.StringCommand );
		client._netchan.Message.WriteString( $"setinfo \"{key}\" \"{value}\"\n" );
	}

	private static string Printable( string value )
	{
		if ( !Unicode.Validate( value ) )
		{
			var builder = new StringBuilder();

			foreach ( var character in value )
			{
				if ( (byte)(character - 0x20) < 0x5F )
					builder.Append( character );
			}

			value = builder.ToString();
		}

		return value.Length == 0 ? "empty" : value;
	}

	private static bool IsSpace( int character )
	{
		return character is (>= 0x09 and <= 0x0D) or 0x20 or 0x85 or 0xA0 or 0x1680 or 0x180E or (>= 0x2000 and <= 0x200A) or 0x2028 or 0x2029 or 0x202F or 0x205F or 0x3000;
	}

	private static bool IsMeanSpace( int character )
	{
		return character is (>= 0x80 and <= 0xA0) or 0x34F or (>= 0x2000 and <= 0x200D) or 0x2028 or 0x2029 or 0x202F or 0x2060 or 0xFEFF or 0xFFFC;
	}

	private static bool IsStripped( int character )
	{
		return character is < 0x20 or (>= 0x202A and <= 0x202E) or (>= 0x206A and <= 0x206F) or 0x85;
	}

	private static string StripWhitespace( string value )
	{
		var wide = Encoding.UTF8.GetString( Encoding.Latin1.GetBytes( value ) );
		var buffer = new char[wide.Length + 1];

		wide.CopyTo( 0, buffer, 0, wide.Length );

		var write = 0;

		for ( var read = 0; read < wide.Length; read++ )
		{
			if ( !IsStripped( buffer[read] ) )
				buffer[write++] = buffer[read];
		}

		var removed = write != wide.Length;

		buffer[write] = '\0';

		var trimmed = false;
		var end = wide.Length - 1;

		while ( end >= 0 && (IsSpace( buffer[end] ) || IsMeanSpace( buffer[end] )) )
		{
			buffer[end--] = '\0';
			trimmed = true;
		}

		var start = 0;

		while ( start < end && (IsSpace( buffer[start] ) || IsMeanSpace( buffer[start] )) )
		{
			start++;
			trimmed = true;
		}

		if ( !removed && !trimmed )
			return value;

		var length = Array.IndexOf( buffer, '\0', start ) - start;
		var bytes = Encoding.UTF8.GetBytes( buffer, start, length );

		return Encoding.Latin1.GetString( bytes, 0, Math.Min( bytes.Length, value.Length ) );
	}
}
