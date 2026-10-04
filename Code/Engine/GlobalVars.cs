namespace GoldSrc;

public sealed class GlobalVars
{
	public float Time { get; set; }
	public float FrameTime { get; set; }
	public float ForceRetouch { get; set; }
	public float Teamplay { get; set; }
	public float FoundSecrets { get; set; }
	public Edict TraceEnt { get; set; }
	public Trace Trace { get; set; }
	public int TraceFlags { get; set; }
	public string MapName { get; set; }
	public string StartSpot { get; set; }
	public Vector3 LandmarkOffset { get; set; }
	public Vector3 Forward { get; set; }
	public Vector3 Up { get; set; }
	public Vector3 Right { get; set; }
	public int CdAudioTrack { get; set; }
	public SaveRestoreData SaveData { get; set; }

	public void MakeVectors( Vector3 angles )
	{
		MathLib.AngleVectors( angles, out var forward, out var right, out var up );

		Forward = forward;
		Right = right;
		Up = up;
	}
}
