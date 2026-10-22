using System;
using System.Globalization;

namespace GoldSrc;

public static class Cvar
{
	private const string VolumeName = "volume";
	private const string SensitivityName = "sensitivity";
	private const string HostNameName = "hostname";
	private const string VoiceLoopbackName = "voice_loopback";
	private const string ServerPasswordName = "sv_password";
	private const string PasswordName = "password";

	public static float GetFloat( string name ) => Common.Atof( GetString( name ) );

	public static void ReadArchive()
	{
		SoundEngine.Volume = Game.Cookies.Get( VolumeName, SoundEngine.Volume );
		Client.Sensitivity = Game.Cookies.Get( SensitivityName, Client.Sensitivity );
	}

	public static bool Exists( string name )
	{
		return name.Equals( VolumeName, StringComparison.OrdinalIgnoreCase )
			|| name.Equals( SensitivityName, StringComparison.OrdinalIgnoreCase )
			|| name.Equals( HostNameName, StringComparison.OrdinalIgnoreCase )
			|| name.Equals( VoiceLoopbackName, StringComparison.OrdinalIgnoreCase )
			|| name.Equals( ServerPasswordName, StringComparison.OrdinalIgnoreCase )
			|| name.Equals( PasswordName, StringComparison.OrdinalIgnoreCase )
			|| ConsoleSystem.GetValue( name ) is not null;
	}

	public static string GetString( string name )
	{
		if ( name.Equals( VolumeName, StringComparison.OrdinalIgnoreCase ) )
			return FormatValue( SoundEngine.Volume );

		if ( name.Equals( SensitivityName, StringComparison.OrdinalIgnoreCase ) )
			return FormatValue( Client.Sensitivity );

		if ( name.Equals( HostNameName, StringComparison.OrdinalIgnoreCase ) )
			return Server.HostName;

		if ( name.Equals( VoiceLoopbackName, StringComparison.OrdinalIgnoreCase ) )
			return FormatValue( Client.VoiceLoopback );

		if ( name.Equals( ServerPasswordName, StringComparison.OrdinalIgnoreCase ) )
			return Server.SvPassword;

		if ( name.Equals( PasswordName, StringComparison.OrdinalIgnoreCase ) )
			return Client.Password;

		return ConsoleSystem.GetValue( name, "" );
	}

	public static void Set( string name, string value )
	{
		if ( name.Equals( VolumeName, StringComparison.OrdinalIgnoreCase ) )
		{
			SoundEngine.Volume = Common.Atof( value );

			Game.Cookies.Set( VolumeName, SoundEngine.Volume );
			return;
		}

		if ( name.Equals( SensitivityName, StringComparison.OrdinalIgnoreCase ) )
		{
			Client.Sensitivity = Common.Atof( value );

			Game.Cookies.Set( SensitivityName, Client.Sensitivity );
			return;
		}

		if ( name.Equals( HostNameName, StringComparison.OrdinalIgnoreCase ) )
		{
			Server.HostName = value;
			return;
		}

		if ( name.Equals( VoiceLoopbackName, StringComparison.OrdinalIgnoreCase ) )
		{
			Client.VoiceLoopback = Common.Atof( value );
			return;
		}

		if ( name.Equals( ServerPasswordName, StringComparison.OrdinalIgnoreCase ) )
		{
			Server.SvPassword = value;
			return;
		}

		if ( name.Equals( PasswordName, StringComparison.OrdinalIgnoreCase ) )
		{
			Client.Password = value;
			return;
		}

		if ( ConsoleSystem.GetValue( name ) is null )
		{
			Con.DPrintf( $"Cvar_Set: variable {name} not found\n" );
			return;
		}

		ConsoleSystem.SetValue( name, value );
	}

	public static void SetValue( string name, float value )
	{
		Set( name, FormatValue( value ) );
	}

	public static string FormatValue( float value )
	{
		return MathF.Abs( value - (int)value ) < 0.000001f
			? ((int)value).ToString( CultureInfo.InvariantCulture )
			: value.ToString( "F6", CultureInfo.InvariantCulture );
	}
}
