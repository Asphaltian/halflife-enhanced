using System;

namespace GoldSrc.GameUI;

public static class ModInfo
{
	public static bool AnimatedTitle => GetString( "animated_title" ) == "1";

	public static bool HdBackground => GetString( "hd_background" ) == "1";

	public static bool DetailedTextures => GetString( "detailed_textures" ) == "1";

	public static bool IsMultiplayerOnly => GetString( "type" ).Equals( "multiplayer_only", StringComparison.OrdinalIgnoreCase );

	public static bool IsSinglePlayerOnly => GetString( "type" ).Equals( "singleplayer_only", StringComparison.OrdinalIgnoreCase );

	public static bool NoModels => GetString( "nomodels" ).Equals( "1", StringComparison.OrdinalIgnoreCase );

	public static bool NoHiModels => GetString( "nohimodel" ).Equals( "1", StringComparison.OrdinalIgnoreCase );

	public static string GameName => GetString( "game" );

	public static string FallbackDir => GetString( "fallback_dir" );

	public static string HiddenMaps => GetString( "mpfilter" );

	public static bool FallbackMaps => !HasKey( "fallback_maps" ) || GetString( "fallback_maps" ).Equals( "1", StringComparison.OrdinalIgnoreCase );

	private static bool HasKey( string key )
	{
		if ( Server.LoadMountText( "liblist.gam" ) is not { } text )
			return false;

		var position = 0;

		while ( Common.Parse( text, ref position ) is { } name && Common.Parse( text, ref position ) is not null )
		{
			if ( name.Equals( key, StringComparison.OrdinalIgnoreCase ) )
				return true;
		}

		return false;
	}

	public static string GetString( string key )
	{
		if ( Server.LoadMountText( "liblist.gam" ) is not { } text )
			return "";

		var position = 0;

		while ( Common.Parse( text, ref position ) is { } name && Common.Parse( text, ref position ) is { } value )
		{
			if ( name.Equals( key, StringComparison.OrdinalIgnoreCase ) )
				return value;
		}

		return "";
	}
}
