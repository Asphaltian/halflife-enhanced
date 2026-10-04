using System;

namespace GoldSrc;

partial class SoundEngine
{
	private const int MaxVoxSentences = 16;
	private const int SentenceBits = 8;
	private const string SentenceFile = "sound/sentences.txt";

	private readonly bool[] _sentenceUsed = new bool[MaxVoxSentences];
	private readonly Vox _vox = new( Server.LoadMountFile );

	private void ReadSentenceFile()
	{
		if ( Server.LoadMountText( SentenceFile ) is not { } file )
		{
			_vox.Sentences.Clear();

			Log.Info( $"VOX_ReadSentenceFile: Couldn't load {SentenceFile}" );
			return;
		}

		_vox.ReadSentenceFile( file );
	}

	public string SentenceName( int index )
	{
		return index >= 0 && index < _vox.Sentences.Count ? _vox.Sentences[index].Name : null;
	}

	private SoundFile RenderSentence( string name, string text )
	{
		if ( _vox.Render( text, out var rate ) is not { } samples )
			return null;

		var data = new byte[samples.Length * sizeof( short )];

		for ( var i = 0; i < samples.Length; i++ )
		{
			data[i * 2] = (byte)samples[i];
			data[(i * 2) + 1] = (byte)(samples[i] >> 8);
		}

		return SoundFile.FromPcm( $"{Server.GameDirectory}/{SentenceFile}/{name}", data, new SoundFile.PcmOptions { Channels = 1, Rate = (uint)rate, Bits = 16 } );
	}

	private int VoxFindFreeSentence()
	{
		for ( var i = 0; i < MaxVoxSentences; i++ )
		{
			if ( !_sentenceUsed[i] )
				return i;
		}

		Log.Info( "Sentence or Pitch shift ignored. > 16 playing!" );

		return -1;
	}

	private void VoxMakeSingleWordSentence( SfxChannel channel, int pitch )
	{
		var sentence = VoxFindFreeSentence();

		if ( sentence < 0 )
		{
			channel.Pitch = 100;
			channel.ISentence = -1;
			return;
		}

		_sentenceUsed[sentence] = true;

		channel.Pitch = pitch;
		channel.ISentence = sentence;
	}

	private bool VoxLoadSound( SfxChannel channel, string name )
	{
		string found = null;
		string text = null;

		foreach ( var (sentenceName, sentenceText) in _vox.Sentences )
		{
			if ( !sentenceName.Equals( name, StringComparison.OrdinalIgnoreCase ) )
				continue;

			found = sentenceName;
			text = sentenceText;
			break;
		}

		if ( found is null )
		{
			Log.Info( $"VOX_LoadSound: no sentence named {name}" );
			return false;
		}

		var sfx = FindName( $"!{found}" );

		if ( sfx.File is not { IsValid: true } )
		{
			var file = RenderSentence( found, text );

			file?.Preload();

			if ( file is not { IsValid: true, IsLoaded: true } )
				return false;

			sfx.File = file;
			sfx.Loop = false;
			sfx.Duration = file.Duration;
			sfx.Bits = SentenceBits;
			sfx.Rate = file.Rate;
		}

		var sentence = VoxFindFreeSentence();

		if ( sentence < 0 )
			return false;

		_sentenceUsed[sentence] = true;

		channel.ISentence = sentence;
		channel.Sfx = sfx;
		channel.PitchShift = true;

		PlayChannel( channel, sfx.File, sfx.Duration );

		return true;
	}
}
