using System;
using System.Globalization;
using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "light" )]
[LinkEntityToClass( "light_spot" )]
public class Light : PointEntity
{
	private const int SpawnFlagStartOff = 1;
	private const int FirstSwitchableStyle = 32;

	[Save( FieldType.Integer )]
	private int _style;
	[Save( FieldType.String )]
	private string _pattern;

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "style":
				_style = Util.Atoi( value );
				return true;
			case "pitch":
				Angles = Angles.WithX( Util.Atof( value ) );
				return true;
			case "pattern":
				_pattern = Server.AllocString( value );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public override void Spawn()
	{
		if ( string.IsNullOrEmpty( TargetName ) )
		{
			Server.FreeEdict( this );
			return;
		}

		if ( _style < FirstSwitchableStyle )
			return;

		if ( (SpawnFlags & SpawnFlagStartOff) != 0 )
			Server.LightStyle( _style, "a" );
		else if ( !string.IsNullOrEmpty( _pattern ) )
			Server.LightStyle( _style, _pattern );
		else
			Server.LightStyle( _style, "m" );
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( _style < FirstSwitchableStyle )
			return;

		if ( !ShouldToggle( useType, (SpawnFlags & SpawnFlagStartOff) == 0 ) )
			return;

		if ( (SpawnFlags & SpawnFlagStartOff) != 0 )
		{
			Server.LightStyle( _style, !string.IsNullOrEmpty( _pattern ) ? _pattern : "m" );
			SpawnFlags &= ~SpawnFlagStartOff;
		}
		else
		{
			Server.LightStyle( _style, "a" );
			SpawnFlags |= SpawnFlagStartOff;
		}
	}
}

[LinkEntityToClass( "light_environment" )]
public class EnvLight : Light
{
	public override bool KeyValue( string key, string value )
	{
		if ( key != "_light" )
			return base.KeyValue( key, value );

		var values = new int[4];
		var count = 0;

		foreach ( var token in value.Split( (char[])null, StringSplitOptions.RemoveEmptyEntries ) )
		{
			if ( count == values.Length || !int.TryParse( token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out values[count] ) )
				break;

			count++;
		}

		var (r, g, b, v) = (values[0], values[1], values[2], values[3]);

		if ( count == 1 )
		{
			g = b = r;
		}
		else if ( count == 4 )
		{
			r = (int)(r * (v / 255.0));
			g = (int)(g * (v / 255.0));
			b = (int)(b * (v / 255.0));
		}

		r = (int)(Math.Pow( r / 114.0, 0.6 ) * 264);
		g = (int)(Math.Pow( g / 114.0, 0.6 ) * 264);
		b = (int)(Math.Pow( b / 114.0, 0.6 ) * 264);

		Cvar.Set( "sv_skycolor_r", r.ToString( CultureInfo.InvariantCulture ) );
		Cvar.Set( "sv_skycolor_g", g.ToString( CultureInfo.InvariantCulture ) );
		Cvar.Set( "sv_skycolor_b", b.ToString( CultureInfo.InvariantCulture ) );

		return true;
	}

	public override void Spawn()
	{
		Util.MakeAimVectors( Angles, Server.Globals );

		var forward = Server.Globals.Forward;

		Cvar.Set( "sv_skyvec_x", forward.x.ToString( "F6", CultureInfo.InvariantCulture ) );
		Cvar.Set( "sv_skyvec_y", forward.y.ToString( "F6", CultureInfo.InvariantCulture ) );
		Cvar.Set( "sv_skyvec_z", forward.z.ToString( "F6", CultureInfo.InvariantCulture ) );

		base.Spawn();
	}
}
