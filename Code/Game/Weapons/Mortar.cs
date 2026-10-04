using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "func_mortar_field" )]
public class FuncMortarField : BaseToggle
{
	[Save( FieldType.String )]
	private string _controllerX;
	[Save( FieldType.String )]
	private string _controllerY;
	[Save( FieldType.Float )]
	private float _spread;
	[Save( FieldType.Integer )]
	private int _count;
	[Save( FieldType.Integer )]
	private int _control;

	public override ObjectCaps ObjectCaps => base.ObjectCaps & ~ObjectCaps.AcrossTransition;

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "m_iszXController":
				_controllerX = Server.AllocString( value );
				return true;
			case "m_iszYController":
				_controllerY = Server.AllocString( value );
				return true;
			case "m_flSpread":
				_spread = Util.Atof( value );
				return true;
			case "m_fControl":
				_control = Util.Atoi( value );
				return true;
			case "m_iCount":
				_count = Util.Atoi( value );
				return true;
			default:
				return false;
		}
	}

	public override void Spawn()
	{
		Solid = Solid.Not;
		SetModel( ModelName );
		MoveType = MoveType.None;
		Effects |= Effects.NoDraw;
		SetUse( FieldUse );
		Precache();
	}

	public override void Precache()
	{
		Server.PrecacheSound( "weapons/mortar.wav" );
		Server.PrecacheSound( "weapons/mortarhit.wav" );
		Server.PrecacheModel( "sprites/lgtning.spr" );
	}

	public void FieldUse( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		var start = new Vector3( Server.RandomFloat( Mins.x, Maxs.x ), Server.RandomFloat( Mins.y, Maxs.y ), Maxs.z );

		switch ( _control )
		{
			case 1:
				if ( activator is not null )
				{
					start.x = activator.Origin.x;
					start.y = activator.Origin.y;
				}

				break;

			case 2:
				if ( !string.IsNullOrEmpty( _controllerX ) && Util.FindEntityByTargetname( null, _controllerX ) is { } controllerX )
					start.x = Mins.x + (controllerX.IdealYaw * Size.x);

				if ( !string.IsNullOrEmpty( _controllerY ) && Util.FindEntityByTargetname( null, _controllerY ) is { } controllerY )
					start.y = Mins.y + (controllerY.IdealYaw * Size.y);

				break;
		}

		var pitch = Server.RandomLong( 95, 124 );

		Sound.EmitDyn( this, SoundChannel.Voice, "weapons/mortar.wav", 1f, Server.AttenuationNone, 0, pitch );

		var time = 2.5f;

		for ( var i = 0; i < _count; i++ )
		{
			var spot = start;

			spot.x += Server.RandomFloat( -_spread, _spread );
			spot.y += Server.RandomFloat( -_spread, _spread );

			var trace = Util.TraceLine( spot, spot + (new Vector3( 0f, 0f, -1f ) * 4096f), IgnoreMonsters, this );

			var mortar = Create( "monster_mortar", trace.EndPos, Vector3.Zero, activator );

			mortar.NextThink = Server.Globals.Time + time;
			time += Server.RandomFloat( 0.2f, 0.5f );

			if ( i == 0 )
				SoundEnt.InsertSound( WorldSound.Danger, trace.EndPos, 400, 0.3f );
		}
	}
}

[LinkEntityToClass( "monster_mortar" )]
public class Mortar : Grenade
{
	private int _spriteTexture;

	public override void Spawn()
	{
		MoveType = MoveType.None;
		Solid = Solid.Not;

		Damage = 200f;

		SetThink( MortarExplode );
		NextThink = 0f;

		Precache();
	}

	public override void Precache()
	{
		_spriteTexture = Server.PrecacheModel( "sprites/lgtning.spr" );
	}

	public void MortarExplode()
	{
		Server.MessageBegin( MessageDest.Broadcast, Server.TempEntityMessage );
		Server.WriteByte( TempEntity.BeamPoints );
		Server.WriteCoord( Origin.x );
		Server.WriteCoord( Origin.y );
		Server.WriteCoord( Origin.z );
		Server.WriteCoord( Origin.x );
		Server.WriteCoord( Origin.y );
		Server.WriteCoord( Origin.z + 1024f );
		Server.WriteShort( _spriteTexture );
		Server.WriteByte( 0 );
		Server.WriteByte( 0 );
		Server.WriteByte( 1 );
		Server.WriteByte( 40 );
		Server.WriteByte( 0 );
		Server.WriteByte( 255 );
		Server.WriteByte( 160 );
		Server.WriteByte( 100 );
		Server.WriteByte( 128 );
		Server.WriteByte( 0 );
		Server.MessageEnd();

		var trace = Util.TraceLine( Origin + new Vector3( 0f, 0f, 1024f ), Origin - new Vector3( 0f, 0f, 1024f ), DontIgnoreMonsters, this );

		Explode( trace, DamageBlast | DamageMortar );
		Util.ScreenShake( trace.EndPos, 25f, 150f, 1f, 750f );
	}
}
