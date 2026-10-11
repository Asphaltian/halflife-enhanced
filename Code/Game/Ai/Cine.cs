using GoldSrc;

namespace HalfLife;

public class LegacyCineMonster : BaseMonster
{
	protected void CineSpawn( string model )
	{
		Server.PrecacheModel( model );
		SetModel( model );
		SetSize( new Vector3( -16f, -16f, 0f ), new Vector3( 16f, 16f, 64f ) );

		Solid = Solid.SlideBox;
		MoveType = MoveType.Step;
		Effects = 0;
		Health = 1f;
		YawSpeed = 10f;

		Sequence = Impulse;
		ResetSequenceInfo();
		FrameRate = 0f;

		MonsterBloodColor = BloodColorRed;

		if ( string.IsNullOrEmpty( TargetName ) )
		{
			SetThink( CineThink );
			NextThink += 1f;
		}
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		AnimTime = 0f;
		SetThink( CineThink );
		NextThink = Server.Globals.Time;
	}

	public void CineThink()
	{
		if ( AnimTime == 0f )
			ResetSequenceInfo();

		NextThink = (float)(Server.Globals.Time + 1.0);

		if ( SpawnFlags != 0 && SequenceFinished )
		{
			SetThink( SubRemove );
			return;
		}

		StudioFrameAdvance();
	}
}

[LinkEntityToClass( "monster_cine_scientist" )]
public class CineScientist : LegacyCineMonster
{
	public override void Spawn() => CineSpawn( "models/cine-scientist.mdl" );
}

[LinkEntityToClass( "monster_cine2_scientist" )]
public class Cine2Scientist : LegacyCineMonster
{
	public override void Spawn() => CineSpawn( "models/cine2-scientist.mdl" );
}

[LinkEntityToClass( "monster_cine_panther" )]
public class CinePanther : LegacyCineMonster
{
	public override void Spawn() => CineSpawn( "models/cine-panther.mdl" );
}

[LinkEntityToClass( "monster_cine_barney" )]
public class CineBarney : LegacyCineMonster
{
	public override void Spawn() => CineSpawn( "models/cine-barney.mdl" );
}

[LinkEntityToClass( "monster_cine2_hvyweapons" )]
public class Cine2HeavyWeapons : LegacyCineMonster
{
	public override void Spawn() => CineSpawn( "models/cine2_hvyweapons.mdl" );
}

[LinkEntityToClass( "monster_cine2_slave" )]
public class Cine2Slave : LegacyCineMonster
{
	public override void Spawn() => CineSpawn( "models/cine2_slave.mdl" );
}

[LinkEntityToClass( "monster_cine3_scientist" )]
public class Cine3Scientist : LegacyCineMonster
{
	public override void Spawn() => CineSpawn( "models/cine3-scientist.mdl" );
}

[LinkEntityToClass( "monster_cine3_barney" )]
public class Cine3Barney : LegacyCineMonster
{
	public override void Spawn() => CineSpawn( "models/cine3-barney.mdl" );
}

[LinkEntityToClass( "cine_blood" )]
public class CineBlood : BaseEntity
{
	public void BloodGush()
	{
		var globals = Server.Globals;

		NextThink = (float)(globals.Time + 0.1);

		globals.MakeVectors( Angles );

		if ( Health-- < 0f )
			Server.FreeEdict( this );

		if ( Server.RandomFloat( 0f, 1f ) < 0.7 )
			Util.BloodDrips( Origin, Util.RandomBloodVector(), BloodColorRed, 10 );
		else
			Util.BloodStream( Origin, Util.RandomBloodVector(), BloodColorRed, Server.RandomLong( 50, 150 ) );

		if ( Server.RandomFloat( 0f, 1f ) >= 0.75 )
			return;

		var splatDirection = new Vector3( 0f, 0f, -1f );

		splatDirection += (float)(Server.RandomFloat( -1f, 1f ) * 0.6) * globals.Right;
		splatDirection += (float)(Server.RandomFloat( -1f, 1f ) * 0.6) * globals.Forward;

		var trace = Util.TraceLine( Origin + new Vector3( 0f, 0f, 64f ), Origin + (splatDirection * 256f), IgnoreMonsters, this );

		if ( trace.Fraction != 1.0 )
			Util.BloodDecalTrace( trace, BloodColorRed );
	}

	public void BloodStart( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		SetThink( BloodGush );
		NextThink = Server.Globals.Time;
	}

	public override void Spawn()
	{
		Solid = Solid.Not;
		SetUse( BloodStart );
		Health = 20f;
	}
}
