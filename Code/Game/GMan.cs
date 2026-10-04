using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "monster_gman" )]
public class GMan : BaseMonster
{
	private BaseEntity _player;
	[Save( FieldType.EHandle )]
	private BaseEntity _talkTarget;
	[Save( FieldType.Time )]
	private float _talkTime;

	public override int Classify() => ClassNone;

	public override void SetYawSpeed() => YawSpeed = 90f;

	public override int SoundMask() => 0;

	public override void Spawn()
	{
		Precache();

		SetModel( "models/gman.mdl" );
		SetSize( HumanHullMin, HumanHullMax );

		Solid = Solid.SlideBox;
		MoveType = MoveType.Step;
		MonsterBloodColor = DontBleed;
		Health = 100f;
		FieldOfView = 0.5f;
		MonsterState = MonsterState.None;

		MonsterInit();
	}

	public override void Precache() => Server.PrecacheModel( "models/gman.mdl" );

	public override void StartTask( Task task )
	{
		if ( task.Id == TaskId.Wait && !_player.IsValid() )
			_player = Util.FindEntityByClassname( null, "player" );

		base.StartTask( task );
	}

	private void LookAt( BaseEntity target )
	{
		var yaw = VecToYaw( target.Origin - Origin ) - Angles.y;

		if ( yaw > 180f )
			yaw -= 360f;

		if ( yaw < -180f )
			yaw += 360f;

		SetBoneController( 0, yaw );
	}

	public override void RunTask( Task task )
	{
		if ( task.Id != TaskId.Wait )
			SetBoneController( 0, 0f );
		else if ( _talkTime > Server.Globals.Time && _talkTarget.IsValid() )
			LookAt( _talkTarget );
		else if ( _player.IsValid() && Sequence == 0 )
			LookAt( _player );
		else
			SetBoneController( 0, 0f );

		base.RunTask( task );
	}

	public override bool TakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType )
	{
		Health = MaxHealth / 2f;

		if ( damage > 0f )
			SetConditions( ConditionLightDamage );

		if ( damage >= 20f )
			SetConditions( ConditionHeavyDamage );

		return true;
	}

	public override void TraceAttack( BaseEntity attacker, float damage, Vector3 direction, in Trace trace, int damageType )
	{
		Util.Ricochet( trace.EndPos, 1f );
		Combat.AddMultiDamage( attacker, this, damage, damageType );
	}

	public override void PlayScriptedSentence( string sentence, float duration, float volume, float attenuation, bool concurrent, BaseEntity listener )
	{
		base.PlayScriptedSentence( sentence, duration, volume, attenuation, concurrent, listener );

		_talkTime = Server.Globals.Time + duration;
		_talkTarget = listener;
	}
}
