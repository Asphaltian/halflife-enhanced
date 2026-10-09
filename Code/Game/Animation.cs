using System;
using GoldSrc;
using GoldSrc.Studio;

namespace HalfLife;

public struct MonsterEvent
{
	public int Event;
	public string Options;
}

public static class Animation
{
	public const int ActivityNotAvailable = -1;

	public const int EventSpecific = 0;
	public const int EventScripted = 1000;
	public const int EventShared = 2000;
	public const int EventClient = 5000;

	public static bool ExtractBbox( File model, int sequence, out Vector3 mins, out Vector3 maxs )
	{
		mins = default;
		maxs = default;

		if ( model is null )
			return false;

		mins = model.Sequences[sequence].BBMin;
		maxs = model.Sequences[sequence].BBMax;

		return true;
	}

	public static int LookupActivity( File model, int activity )
	{
		if ( model is null )
			return 0;

		var weightTotal = 0;
		var sequence = ActivityNotAvailable;

		for ( var i = 0; i < model.Sequences.Length; i++ )
		{
			if ( model.Sequences[i].Activity != activity )
				continue;

			weightTotal += model.Sequences[i].ActWeight;

			if ( weightTotal == 0 || Server.RandomLong( 0, weightTotal - 1 ) < model.Sequences[i].ActWeight )
				sequence = i;
		}

		return sequence;
	}

	public static int LookupActivityHeaviest( File model, int activity )
	{
		if ( model is null )
			return 0;

		var weight = 0;
		var sequence = ActivityNotAvailable;

		for ( var i = 0; i < model.Sequences.Length; i++ )
		{
			if ( model.Sequences[i].Activity == activity && model.Sequences[i].ActWeight > weight )
			{
				weight = model.Sequences[i].ActWeight;
				sequence = i;
			}
		}

		return sequence;
	}

	public static void GetEyePosition( File model, ref Vector3 eyePosition )
	{
		if ( model is null )
		{
			Util.Alert( AlertType.Console, "GetEyePosition() Can't get pstudiohdr ptr!" );
			return;
		}

		eyePosition = model.Header.EyePosition;
	}

	public static int LookupSequence( File model, string label )
	{
		if ( model is null )
			return 0;

		for ( var i = 0; i < model.Sequences.Length; i++ )
		{
			if ( string.Equals( model.Sequences[i].Label, label, StringComparison.OrdinalIgnoreCase ) )
				return i;
		}

		return -1;
	}

	public static bool IsSoundEvent( int eventNumber ) => eventNumber is ScriptEvent.Sound or ScriptEvent.SoundVoice;

	public static void SequencePrecache( Server server, File model, string sequenceName )
	{
		var index = LookupSequence( model, sequenceName );

		if ( index < 0 || index >= model.Sequences.Length )
			return;

		foreach ( var sequenceEvent in model.Events[index] )
		{
			if ( sequenceEvent.Index >= EventClient || !IsSoundEvent( sequenceEvent.Index ) )
				continue;

			if ( sequenceEvent.Options.Length == 0 )
				Util.Alert( AlertType.Error, $"Bad sound event {sequenceEvent.Index} in sequence {model.Header.Name} :: {sequenceName} (sound is \"{sequenceEvent.Options}\")" );

			server.PrecacheSound( Server.AllocString( sequenceEvent.Options ) );
		}
	}

	public static void GetSequenceInfo( File model, Edict entity, ref float frameRate, ref float groundSpeed )
	{
		if ( model is null )
			return;

		if ( entity.Sequence >= model.Sequences.Length )
		{
			frameRate = 0f;
			groundSpeed = 0f;
			return;
		}

		ref readonly var sequence = ref model.Sequences[entity.Sequence];

		if ( sequence.NumFrames > 1 )
		{
			var movement = sequence.LinearMovement;

			frameRate = 256 * sequence.Fps / (sequence.NumFrames - 1);
			groundSpeed = (float)Math.Sqrt( (movement.x * movement.x) + (movement.y * movement.y) + (movement.z * movement.z) );
			groundSpeed = groundSpeed * sequence.Fps / (sequence.NumFrames - 1);
		}
		else
		{
			frameRate = 256f;
			groundSpeed = 0f;
		}
	}

	public static SequenceFlags GetSequenceFlags( File model, Edict entity )
	{
		if ( model is null || entity.Sequence >= model.Sequences.Length )
			return 0;

		return model.Sequences[entity.Sequence].Flags;
	}

	public static int GetAnimationEvent( File model, Edict entity, ref MonsterEvent monsterEvent, float start, float end, int index )
	{
		if ( model is null || entity.Sequence >= model.Sequences.Length )
			return 0;

		ref readonly var sequence = ref model.Sequences[entity.Sequence];
		var events = model.Events[entity.Sequence];

		if ( sequence.NumEvents == 0 || index > sequence.NumEvents )
			return 0;

		if ( sequence.NumFrames > 1 )
		{
			start = (float)(start * ((sequence.NumFrames - 1) / 256.0));
			end = (float)(end * ((sequence.NumFrames - 1) / 256.0));
		}
		else
		{
			start = 0f;
			end = 1f;
		}

		for ( ; index < sequence.NumEvents; index++ )
		{
			if ( events[index].Index >= EventClient )
				continue;

			if ( (events[index].Frame >= start && events[index].Frame < end)
				|| ((sequence.Flags & SequenceFlags.Looping) == SequenceFlags.Looping && end >= sequence.NumFrames - 1 && events[index].Frame < end - sequence.NumFrames + 1) )
			{
				monsterEvent.Event = events[index].Index;
				monsterEvent.Options = events[index].Options;
				return index + 1;
			}
		}

		return 0;
	}

	public static float SetController( File model, Edict entity, int controller, float value )
	{
		if ( model is null )
			return value;

		var index = Array.FindIndex( model.BoneControllers, x => x.Index == controller );
		if ( index < 0 )
			return value;

		ref readonly var boneController = ref model.BoneControllers[index];

		if ( (boneController.Type & (MotionFlags.XR | MotionFlags.YR | MotionFlags.ZR)) != 0 )
		{
			if ( boneController.End < boneController.Start )
				value = -value;

			if ( boneController.Start + 359.0 >= boneController.End )
			{
				if ( value > ((boneController.Start + boneController.End) / 2.0) + 180 )
					value -= 360f;
				if ( value < ((boneController.Start + boneController.End) / 2.0) - 180 )
					value += 360f;
			}
			else
			{
				if ( value > 360f )
					value = (float)(value - ((int)(value / 360.0) * 360.0));
				else if ( value < 0f )
					value = (float)(value + ((int)((value / -360.0) + 1) * 360.0));
			}
		}

		var setting = (int)(255 * (value - boneController.Start) / (boneController.End - boneController.Start));

		setting = Math.Clamp( setting, 0, 255 );
		entity.Controller[controller] = (byte)setting;

		return (float)((setting * (1.0 / 255.0) * (boneController.End - boneController.Start)) + boneController.Start);
	}

	public static float SetBlending( File model, Edict entity, int blender, float value )
	{
		if ( model is null )
			return value;

		ref readonly var sequence = ref model.Sequences[entity.Sequence];

		var type = sequence.BlendType[blender];
		var start = sequence.BlendStart[blender];
		var end = sequence.BlendEnd[blender];

		if ( type == 0 )
			return value;

		if ( (type & (MotionFlags.XR | MotionFlags.YR | MotionFlags.ZR)) != 0 )
		{
			if ( end < start )
				value = -value;

			if ( start + 359.0 >= end )
			{
				if ( value > ((start + end) / 2.0) + 180 )
					value -= 360f;
				if ( value < ((start + end) / 2.0) - 180 )
					value += 360f;
			}
		}

		var setting = (int)(255 * (value - start) / (end - start));

		setting = Math.Clamp( setting, 0, 255 );
		entity.Blending[blender] = (byte)setting;

		return (float)((setting * (1.0 / 255.0) * (end - start)) + start);
	}

	public static int FindTransition( File model, int endingAnim, int goalAnim, ref int direction )
	{
		if ( model is null )
			return goalAnim;

		var sequences = model.Sequences;

		if ( sequences[endingAnim].EntryNode == 0 || sequences[goalAnim].EntryNode == 0 )
			return goalAnim;

		var endNode = direction > 0 ? sequences[endingAnim].ExitNode : sequences[endingAnim].EntryNode;

		if ( endNode == sequences[goalAnim].EntryNode )
		{
			direction = 1;
			return goalAnim;
		}

		var internNode = model.Transitions[((endNode - 1) * model.Header.NumTransitions) + (sequences[goalAnim].EntryNode - 1)];

		if ( internNode == 0 )
			return goalAnim;

		for ( var i = 0; i < sequences.Length; i++ )
		{
			if ( sequences[i].EntryNode == endNode && sequences[i].ExitNode == internNode )
			{
				direction = 1;
				return i;
			}

			if ( sequences[i].NodeFlags != 0 && sequences[i].ExitNode == endNode && sequences[i].EntryNode == internNode )
			{
				direction = -1;
				return i;
			}
		}

		Util.Alert( AlertType.Console, "error in transition graph" );
		return goalAnim;
	}

	public static void SetBodygroup( File model, Edict entity, int group, int value )
	{
		if ( model is null || group > model.BodyParts.Length )
			return;

		ref readonly var bodyPart = ref model.BodyParts[group];

		if ( value >= bodyPart.NumModels )
			return;

		var current = entity.Body / bodyPart.Base % bodyPart.NumModels;

		entity.Body = entity.Body - (current * bodyPart.Base) + (value * bodyPart.Base);
	}

	public static int GetBodygroup( File model, Edict entity, int group )
	{
		if ( model is null || group > model.BodyParts.Length )
			return 0;

		ref readonly var bodyPart = ref model.BodyParts[group];

		if ( bodyPart.NumModels <= 1 )
			return 0;

		return entity.Body / bodyPart.Base % bodyPart.NumModels;
	}
}
