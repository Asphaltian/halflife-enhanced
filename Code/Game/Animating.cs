using System;
using GoldSrc;
using GoldSrc.Studio;

namespace HalfLife;

public class BaseAnimating : BaseDelay
{
	[Save( FieldType.Float )]
	public float SequenceFrameRate;
	[Save( FieldType.Float )]
	public float GroundSpeed;
	[Save( FieldType.Time )]
	public float LastEventCheck;
	[Save( FieldType.Boolean )]
	public bool SequenceFinished;
	[Save( FieldType.Boolean )]
	public bool SequenceLoops;

	public float StudioFrameAdvance( float interval = 0f )
	{
		if ( interval == 0.0 )
		{
			interval = Server.Globals.Time - AnimTime;

			if ( interval <= 0.001 )
			{
				AnimTime = Server.Globals.Time;
				return 0f;
			}
		}

		if ( AnimTime == 0f )
			interval = 0f;

		Frame += interval * SequenceFrameRate * FrameRate;
		AnimTime = Server.Globals.Time;

		if ( Frame < 0.0 || Frame >= 256.0 )
		{
			if ( SequenceLoops )
				Frame = (float)(Frame - ((int)(Frame / 256.0) * 256.0));
			else
				Frame = Frame < 0.0 ? 0f : 255f;

			SequenceFinished = true;
		}

		return interval;
	}

	public int LookupActivity( int activity ) => Animation.LookupActivity( Server.GetModelPtr( this ), activity );

	public int LookupActivityHeaviest( int activity ) => Animation.LookupActivityHeaviest( Server.GetModelPtr( this ), activity );

	public int LookupSequence( string label ) => Animation.LookupSequence( Server.GetModelPtr( this ), label );

	public void ResetSequenceInfo()
	{
		Animation.GetSequenceInfo( Server.GetModelPtr( this ), this, ref SequenceFrameRate, ref GroundSpeed );

		SequenceLoops = (GetSequenceFlags() & SequenceFlags.Looping) != 0;
		AnimTime = Server.Globals.Time;
		FrameRate = 1f;
		SequenceFinished = false;
		LastEventCheck = Server.Globals.Time;
	}

	public SequenceFlags GetSequenceFlags() => Animation.GetSequenceFlags( Server.GetModelPtr( this ), this );

	public void DispatchAnimEvents( float interval = 0.1f )
	{
		var model = Server.GetModelPtr( this );

		if ( model is null )
		{
			Util.Alert( AlertType.AiConsole, "Gibbed monster is thinking!\n" );
			return;
		}

		interval = 0.1f;

		var start = Frame + ((LastEventCheck - AnimTime) * SequenceFrameRate * FrameRate);
		var end = Frame + (interval * SequenceFrameRate * FrameRate);

		LastEventCheck = AnimTime + interval;

		SequenceFinished = false;
		if ( end >= 256f || end <= 0.0 )
			SequenceFinished = true;

		var index = 0;
		var monsterEvent = new MonsterEvent();

		while ( (index = Animation.GetAnimationEvent( model, this, ref monsterEvent, start, end, index )) != 0 )
			HandleAnimEvent( monsterEvent );
	}

	public virtual void HandleAnimEvent( MonsterEvent monsterEvent )
	{
	}

	public float SetBoneController( int controller, float value ) => Animation.SetController( Server.GetModelPtr( this ), this, controller, value );

	public void InitBoneControllers()
	{
		var model = Server.GetModelPtr( this );

		Animation.SetController( model, this, 0, 0f );
		Animation.SetController( model, this, 1, 0f );
		Animation.SetController( model, this, 2, 0f );
		Animation.SetController( model, this, 3, 0f );
	}

	public float SetBlending( int blender, float value ) => Animation.SetBlending( Server.GetModelPtr( this ), this, blender, value );

	public int FindTransition( int endingSequence, int goalSequence, ref int direction )
	{
		return Animation.FindTransition( Server.GetModelPtr( this ), endingSequence, goalSequence, ref direction );
	}

	public int FindTransition( int endingSequence, int goalSequence )
	{
		var direction = 0;
		var sequence = Animation.FindTransition( Server.GetModelPtr( this ), endingSequence, goalSequence, ref direction );

		return direction != 1 ? -1 : sequence;
	}

	public void SetBodygroup( int group, int value ) => Animation.SetBodygroup( Server.GetModelPtr( this ), this, group, value );

	public int GetBodygroup( int group ) => Animation.GetBodygroup( Server.GetModelPtr( this ), this, group );

	public bool ExtractBbox( int sequence, out Vector3 mins, out Vector3 maxs )
	{
		return Animation.ExtractBbox( Server.GetModelPtr( this ), sequence, out mins, out maxs );
	}

	public void SetSequenceBox()
	{
		if ( !ExtractBbox( Sequence, out var mins, out var maxs ) )
			return;

		var yaw = (float)(Angles.y * (Math.PI / 180.0));

		var xVector = new Vector3( (float)Math.Cos( yaw ), (float)Math.Sin( yaw ), 0f );
		var yVector = new Vector3( (float)-Math.Sin( yaw ), (float)Math.Cos( yaw ), 0f );

		Vector3[] bounds = [mins, maxs];

		var rmin = new Vector3( 9999f, 9999f, 9999f );
		var rmax = new Vector3( -9999f, -9999f, -9999f );

		for ( var i = 0; i <= 1; i++ )
		{
			for ( var j = 0; j <= 1; j++ )
			{
				for ( var k = 0; k <= 1; k++ )
				{
					var x = bounds[i].x;
					var y = bounds[j].y;
					var z = bounds[k].z;

					var transformed = new Vector3( (xVector.x * x) + (yVector.x * y), (xVector.y * x) + (yVector.y * y), z );

					rmin = Vector3.Min( rmin, transformed );
					rmax = Vector3.Max( rmax, transformed );
				}
			}
		}

		rmin.z = 0f;
		rmax.z = rmin.z + 1f;

		SetSize( rmin, rmax );
	}
}
