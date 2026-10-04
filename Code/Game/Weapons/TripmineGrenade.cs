using System;
using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "monster_tripmine" )]
public class TripmineGrenade : Grenade
{
	private const int SequenceWorld = 7;

	[Save( FieldType.Time )]
	private float _powerUp;
	[Save( FieldType.Vector )]
	private Vector3 _direction;
	[Save( FieldType.PositionVector )]
	private Vector3 _end;
	[Save( FieldType.Float )]
	private float _beamLength;
	[Save( FieldType.EHandle )]
	private BaseEntity _attachedTo;
	[Save( FieldType.ClassPtr )]
	private Beam _beam;
	[Save( FieldType.PositionVector )]
	private Vector3 _ownerPosition;
	[Save( FieldType.Vector )]
	private Vector3 _ownerAngles;
	[Save( FieldType.Edict )]
	private Edict _realOwner;

	private BaseEntity AttachedTo => _attachedTo.IsValid() ? _attachedTo : null;

	public override void Spawn()
	{
		Precache();

		MoveType = MoveType.Fly;
		Solid = Solid.Not;

		SetModel( "models/v_tripmine.mdl" );
		Frame = 0f;
		Body = 3;
		Sequence = SequenceWorld;
		ResetSequenceInfo();
		FrameRate = 0f;

		SetSize( new Vector3( -8f, -8f, -8f ), new Vector3( 8f, 8f, 8f ) );
		SetOrigin( Origin );

		_powerUp = (SpawnFlags & 1) != 0 ? (float)(Server.Globals.Time + 1.0) : (float)(Server.Globals.Time + 2.5);

		SetThink( PowerupThink );
		NextThink = (float)(Server.Globals.Time + 0.2);

		DamageMode = DamageYes;
		Damage = HalfLifeDll.SkillData.PlrDmgTripmine;
		Health = 1f;

		if ( Owner is not null )
		{
			Sound.Emit( this, SoundChannel.Voice, "weapons/mine_deploy.wav", 1f, Server.AttenuationNormal );
			Sound.Emit( this, SoundChannel.Body, "weapons/mine_charge.wav", 0.2f, Server.AttenuationNormal );

			_realOwner = Owner;
		}

		Util.MakeAimVectors( Angles, Server.Globals );

		_direction = Server.Globals.Forward;
		_end = Origin + (_direction * 2048f);
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/v_tripmine.mdl" );
		Server.PrecacheSound( "weapons/mine_deploy.wav" );
		Server.PrecacheSound( "weapons/mine_activate.wav" );
		Server.PrecacheSound( "weapons/mine_charge.wav" );
	}

	public void WarningThink()
	{
		SetThink( PowerupThink );
		NextThink = (float)(Server.Globals.Time + 1.0);
	}

	private void StopChargeSounds()
	{
		Server.StopSound( this, SoundChannel.Voice, "weapons/mine_deploy.wav" );
		Server.StopSound( this, SoundChannel.Body, "weapons/mine_charge.wav" );
	}

	public void PowerupThink()
	{
		if ( AttachedTo is not { } attachedTo )
		{
			var oldOwner = Owner;

			Owner = null;

			var trace = Util.TraceLine( Origin + (_direction * 8f), Origin - (_direction * 32f), DontIgnoreMonsters, this );

			if ( trace.StartSolid || (oldOwner is not null && trace.Ent == oldOwner) )
			{
				Owner = oldOwner;
				_powerUp = (float)(_powerUp + 0.1);
				NextThink = (float)(Server.Globals.Time + 0.1);
				return;
			}

			if ( trace.Fraction < 1.0 )
			{
				Owner = trace.Ent;
				_attachedTo = (BaseEntity)Owner;
				_ownerPosition = _attachedTo.Origin;
				_ownerAngles = _attachedTo.Angles;
			}
			else
			{
				StopChargeSounds();

				SetThink( SubRemove );
				NextThink = (float)(Server.Globals.Time + 0.1);

				Log.Info( $"WARNING:Tripmine at {Origin.x:0}, {Origin.y:0}, {Origin.z:0} removed" );

				KillBeam();
				return;
			}
		}
		else if ( !MathLib.VectorCompare( _ownerPosition, attachedTo.Origin ) || !MathLib.VectorCompare( _ownerAngles, attachedTo.Angles ) )
		{
			StopChargeSounds();

			if ( Create( "weapon_tripmine", Origin + (_direction * 24f), Angles, null ) is { } mine )
				mine.SpawnFlags |= BasePlayerItem.SpawnFlagNoRespawn;

			SetThink( SubRemove );
			KillBeam();
			NextThink = (float)(Server.Globals.Time + 0.1);
			return;
		}

		if ( Server.Globals.Time > _powerUp )
		{
			Solid = Solid.BBox;
			SetOrigin( Origin );

			MakeBeam();

			Sound.EmitDyn( this, SoundChannel.Voice, "weapons/mine_activate.wav", 0.5f, Server.AttenuationNormal, 1, 75 );
		}

		NextThink = (float)(Server.Globals.Time + 0.1);
	}

	public void KillBeam()
	{
		if ( _beam is null )
			return;

		_beam.Remove();
		_beam = null;
	}

	public void MakeBeam()
	{
		var trace = Util.TraceLine( Origin, _end, DontIgnoreMonsters, this );

		_beamLength = trace.Fraction;

		SetThink( BeamBreakThink );
		NextThink = (float)(Server.Globals.Time + 0.1);

		var end = Origin + (_direction * 2048f * _beamLength);

		_beam = Beam.BeamCreate( Server, GameModels.LaserName, 10 );
		_beam.PointEntInit( end, Index );
		_beam.SetColor( 0, 214, 198 );
		_beam.SetScrollRate( 255 );
		_beam.SetBrightness( 64 );
	}

	public void BeamBreakThink()
	{
		Server.Globals.TraceFlags = Server.TraceSimpleBox;

		var trace = Util.TraceLine( Origin, _end, DontIgnoreMonsters, this );

		if ( _beam is null )
		{
			MakeBeam();

			if ( trace.Ent is not null )
				_attachedTo = trace.Ent as BaseEntity;
		}

		bool blowUp;

		if ( Math.Abs( _beamLength - trace.Fraction ) > 0.001 )
			blowUp = true;
		else if ( AttachedTo is not { } attachedTo )
			blowUp = true;
		else
			blowUp = !MathLib.VectorCompare( _ownerPosition, attachedTo.Origin ) || !MathLib.VectorCompare( _ownerAngles, attachedTo.Angles );

		if ( blowUp )
		{
			Owner = _realOwner;
			Health = 0f;
			Killed( Owner as BaseEntity, GibNormal );
			return;
		}

		NextThink = (float)(Server.Globals.Time + 0.1);
	}

	public override bool TakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType )
	{
		if ( Server.Globals.Time < _powerUp && damage < Health )
		{
			SetThink( SubRemove );
			NextThink = (float)(Server.Globals.Time + 0.1);
			KillBeam();
			return false;
		}

		return base.TakeDamage( inflictor, attacker, damage, damageType );
	}

	public override void Killed( BaseEntity attacker, int gib )
	{
		DamageMode = DamageNo;

		if ( attacker is not null && (attacker.EntityFlags & EntityFlags.Client) == EntityFlags.Client )
			Owner = attacker;

		SetThink( DelayDeathThink );
		NextThink = Server.Globals.Time + Server.RandomFloat( 0.1f, 0.3f );

		Sound.Emit( this, SoundChannel.Body, "common/null.wav", 0.5f, Server.AttenuationNormal );
	}

	public void DelayDeathThink()
	{
		KillBeam();

		var trace = Util.TraceLine( Origin + (_direction * 8f), Origin - (_direction * 64f), DontIgnoreMonsters, this );

		Explode( trace, DamageBlast );
	}
}
