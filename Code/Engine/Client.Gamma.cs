using System;
using System.Threading.Tasks;

namespace GoldSrc;

public sealed partial class Client
{
	private const float MinGamma = 1.8f;
	private const float MaxGamma = 3f;
	private const float MaxBrightness = 100f;
	private const float MaxMultiplayerBrightness = 2f;
	private const int GlNearest = 0x2600;
	private const int GlLinear = 0x2601;

	[SkipHotload]
	private static readonly (string Name, int Minimize, int Maximize)[] TextureModes =
	[
		("GL_NEAREST", 0x2600, 0x2600),
		("GL_LINEAR", 0x2601, 0x2601),
		("GL_NEAREST_MIPMAP_NEAREST", 0x2700, 0x2600),
		("GL_LINEAR_MIPMAP_NEAREST", 0x2701, 0x2601),
		("GL_NEAREST_MIPMAP_LINEAR", 0x2702, 0x2600),
		("GL_LINEAR_MIPMAP_LINEAR", 0x2703, 0x2601)
	];

	private static string _textureMode = "GL_LINEAR_MIPMAP_LINEAR";
	private static float _preferTextureFiltering = 1f;
	private static int _filterMin = 0x2703;
	private static int _filterMax = GlLinear;
	private static float _uploadGamma = 2.2f;
	private static float _uploadTexGamma = 2f;

	[ConVar( "gamma", ConVarFlags.Saved )]
	public static float Gamma { get; set; } = 2.2f;

	[ConVar( "texgamma" )]
	public static float TexGamma { get; set; } = 2f;

	[ConVar( "lightgamma" )]
	public static float LightGamma { get; set; } = 2.5f;

	[ConVar( "brightness", ConVarFlags.Saved )]
	public static float Brightness { get; set; }

	[ConVar( "r_prefertexturefiltering", ConVarFlags.Saved )]
	public static float PreferTextureFiltering
	{
		get => _preferTextureFiltering;
		set
		{
			if ( _preferTextureFiltering == value )
				return;

			_preferTextureFiltering = value;

			TextureMode = value != 0f ? "GL_LINEAR_MIPMAP_LINEAR" : "GL_NEAREST";
		}
	}

	[ConVar( "gl_texturemode", ConVarFlags.Saved )]
	public static string TextureMode
	{
		get => _textureMode;
		set
		{
			if ( _textureMode == value )
				return;

			_textureMode = value;

			var mode = Array.FindIndex( TextureModes, x => x.Name.Equals( value, StringComparison.OrdinalIgnoreCase ) );

			if ( mode < 0 )
			{
				Log.Info( "bad filter name" );
				return;
			}

			_filterMin = TextureModes[mode].Minimize;
			_filterMax = TextureModes[mode].Maximize;

			if ( _preferTextureFiltering == 1f && value.Equals( "GL_NEAREST", StringComparison.OrdinalIgnoreCase ) )
				PreferTextureFiltering = 0f;
		}
	}

	[ConVar( "r_detailtextures", ConVarFlags.Saved )]
	public static float DetailTextures { get; set; }

	[ConVar( "r_detailtexturessupported" )]
	public static float DetailTexturesSupported { get; set; } = 1f;

	[ConCmd( "_sethdmodels" )]
	public static void SetHdModels( params string[] arguments ) => SetSearchPathOption( "hdmodels", arguments );

	[ConCmd( "_setaddons_folder" )]
	public static void SetAddonsFolder( params string[] arguments ) => SetSearchPathOption( "addons_folder", arguments );

	private static void SetSearchPathOption( string key, string[] arguments )
	{
		if ( arguments.Length != 1 )
			return;

		var old = Registry.ReadInt( key ) > 0;

		Registry.WriteInt( key, arguments[0].Equals( "1", StringComparison.OrdinalIgnoreCase ) ? 1 : 0 );

		if ( old != Registry.ReadInt( key ) > 0 )
			Server.SetGameDirectory();
	}

	[ConCmd( "_restart" )]
	public static void Restart()
	{
		var reload = Server.Current is { Active: true } && Current is { MaxClients: 1, InLevel: true };

		if ( reload )
		{
			Cmd.AddText( "save quick\n" );
			Cmd.Execute();
		}

		_ = Restart( reload );
	}

	private static async Task Restart( bool reload )
	{
		Host.Disconnect();
		Server.SetGameDirectory();

		if ( (Gamma != _uploadGamma || TexGamma != _uploadTexGamma) && Sandbox.Mounting.Directory.Get( Server.MountIdent ) is { } mount )
		{
			_uploadGamma = Gamma;
			_uploadTexGamma = TexGamma;

			await mount.RefreshInternal();
		}

		if ( reload )
			Cmd.AddText( "load quick\n" );
	}

	public static int LightGammaTable( float index )
	{
		var light = MathF.Pow( index / 1023f, LightGamma );
		var threshold = 0.125f;

		if ( Brightness > 1f )
		{
			light *= Brightness;
			threshold = 0.05f;
		}
		else if ( Brightness > 0f )
		{
			threshold = 0.125f - (Brightness * Brightness * 0.075f);
		}

		light = light > threshold
			? 0.125f + ((light - threshold) / (1f - threshold) * 0.875f)
			: light / threshold * 0.125f;

		return Math.Clamp( (int)(MathF.Pow( light, 1f / Gamma ) * 1023f), 0, 1023 );
	}

	private void CheckGamma()
	{
		if ( MaxClients > 1 && Brightness > MaxMultiplayerBrightness )
			Brightness = MaxMultiplayerBrightness;

		Gamma = Math.Clamp( Gamma, MinGamma, MaxGamma );
		TexGamma = Math.Clamp( TexGamma, MinGamma, MaxGamma );
		LightGamma = Math.Clamp( LightGamma, MinGamma, MaxGamma );
		Brightness = Math.Clamp( Brightness, 0f, MaxBrightness );

		var attributes = Scene.RenderAttributes;

		attributes.Set( "Gamma", Gamma );
		attributes.Set( "LightGamma", LightGamma );
		attributes.Set( "Brightness", Brightness );
		attributes.Set( "TextureNearest", _filterMax == GlNearest ? 1f : 0f );
		attributes.Set( "TextureNoMips", _filterMin is GlNearest or GlLinear ? 1f : 0f );
	}
}
