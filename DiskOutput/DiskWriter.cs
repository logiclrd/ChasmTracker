using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace ChasmTracker.DiskOutput;

using ChasmTracker.Configurations;
using ChasmTracker.Dialogs;
using ChasmTracker.FileTypes;
using ChasmTracker.MIDI;
using ChasmTracker.Pages;
using ChasmTracker.Playback;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class DiskWriter : IDisposable, IConfigurable<DiskWriterConfiguration> // "disko"
{
	public const int BufferSize = 65536;

	static int OutputRate = 44100;
	static int OutputBits = 16;
	static int OutputChannels = 2;

	// ---------------------------------------------------------------------------

	static byte[]? s_buffer;

	[MemberNotNull(nameof(s_buffer))]
	static void EnsureBuffer(int size)
	{
		if ((s_buffer == null) || (s_buffer.Length < size))
			s_buffer = new byte[size * 2];
	}

	// ---------------------------------------------------------------------------

	public DiskWriter(DiskWriterBackend backend)
	{
		_backend = backend;
	}

	DiskWriterBackend _backend;
	Song? _song;
	Exception? _exception;
	bool _isDisposed = false;

	public Song Song => _song ?? throw new Exception("Invalid state (DiskWriter does not currently have a Song)");

	[MemberNotNullWhen(true, nameof(Exception))]
	public bool HasError => _exception != null;
	public Exception? Exception => _exception;

	public bool IsDisposed => _isDisposed;

	public int Length => _backend.Length;

	public Stream AsStream(bool read) => _backend.AsStream(read);

	// ---------------------------------------------------------------------------

	public void LoadConfiguration(DiskWriterConfiguration config)
	{
		OutputRate = config.Rate;
		OutputBits = config.Bits;
		OutputChannels = config.Channels;
	}

	public void SaveConfiguration(DiskWriterConfiguration config)
	{
		config.Rate = OutputRate;
		config.Bits = OutputBits;
		config.Channels = OutputChannels;
	}

	// ---------------------------------------------------------------------------

		// For triggering abort
	public void SetException(Exception ex)
	{
		_exception = ex;
	}

	public void Truncate(int newLength) => _backend.Truncate(newLength);

	public static void MakeBackup(string path, DiskWriterBackupMode backupMode)
	{
		string? buf = null;

		switch (backupMode)
		{
			case DiskWriterBackupMode.BackupNumbered:
			{
				int n = 1;
				bool failed;

				do
				{
					buf = $"{path}.{n++}~";

					failed = false;

					const int WindowsExistsError = -2146233087;
					const int LinuxExistsError = 17;

					try
					{
						File.Move(path, buf, overwrite: false);
					}
					catch (IOException e) when ((e.HResult != LinuxExistsError) && (e.HResult != WindowsExistsError))
					{
						failed = true;
					}
				} while (failed);

				break;
			}
			case DiskWriterBackupMode.BackupTilde:
			{
				File.Move(path, path + "~", overwrite: true);
				break;
			}
		}
	}

	public void Write(Span<byte> buf)
	{
		_backend.Write(buf);
	}

	public void Silence(int bytes)
	{
#if WRITE_SILENCE
		int blockSize = bytes;

		if (blockSize > 4096)
			blockSize = 4096;

		EnsureBuffer(blockSize);

		s_buffer.Slice(0, blockSize).Clear();

		while (bytes > 0)
		{
			if (blockSize > bytes)
				blockSize = bytes;

			Write(s_buffer.Slice(0, blockSize));

			bytes -= blockSize;
		}
#else
		Seek(bytes, SeekOrigin.Current);
#endif
	}

	public void PutByte(byte b)
	{
		EnsureBuffer(1);

		s_buffer[0] = b;

		_backend.Write(s_buffer.Slice(0, 1));
	}

	public void Seek(long pos, SeekOrigin whence)
	{
		_backend.Seek(pos, whence);
	}

	public long Tell()
	{
		return _backend.Tell();
	}

	public void Align(int bytes)
	{
		long pos = Tell();

		Seek((bytes - (pos % bytes)) % bytes, SeekOrigin.Current);
	}

	public void SetError(Exception ex)
	{
		_exception = ex;
	}

	// ---------------------------------------------------------------------------

	public static DiskWriter Open(string fileName)
	{
		if (string.IsNullOrEmpty(fileName))
			throw new ArgumentException(nameof(fileName));

		var backend = DiskWriterStreamBackend.OpenWrite(fileName);

		return new DiskWriter(backend);
	}

	public static DiskWriter OpenMemory(int estimatedSize = BufferSize)
	{
		if (estimatedSize < 16)
			estimatedSize = 16;

		return new DiskWriter(new DiskWriterMemoryBackend(estimatedSize));
	}

	public void Dispose()
	{
		try
		{
			if (!_isDisposed)
				_backend.Close(DiskWriterBackupMode.NoBackup);

			_isDisposed = true;
		}
		catch { }
	}

	public bool Close(DiskWriterBackupMode backupMode)
	{
		try
		{
			_backend.Close(backupMode);
			_isDisposed = true;
			return true;
		}
		catch (Exception e)
		{
			_exception = e;
			return false;
		}
	}

	// ---------------------------------------------------------------------------

	void ExportSetup(out int bps)
	{
		ExportSetup(out _song, out var savedState, out bps, new NullMIDISink());

		_song.SavedAudioPlaybackState = savedState;
	}

	static void ExportSetup(out Song song, out AudioPlaybackState savedAudioPlaybackState, out int bps, IMIDISink midiSink)
	{
		lock (AudioPlayback.LockScope())
		{
			/* install our own */
			song = Song.CurrentSong.Clone(); /* shadow it */

			// !!! FIXME: We should not be messing with this stuff here!
			song.OPL = null; // Prevent the current_song OPL being closed

			GeneralMIDI.Reset(song, quitting: true);

			// Reset the MIDI stuff to our own...

			song.InitializeMIDI(midiSink);

			song.MultiWrite = null; /* should be null already, but to be sure... */

			song.SetCurrentOrder(0); /* rather indirect way of resetting playback variables */

			savedAudioPlaybackState = AudioPlayback.SaveState();

			AudioPlayback.SetWaveConfig(OutputRate, OutputBits, song.Flags.HasFlag(SongFlags.NoStereo) ? 1 : OutputChannels);

			AudioPlayback.MixFlags |= MixFlags.DirectToDisk | MixFlags.NoBackwardJumps;

			song.RepeatCount = -1; // FIXME do this right
			song.BufferCount = 0;
			song.Flags &= ~(SongFlags.Paused | SongFlags.PatternLoop | SongFlags.EndReached);
			song.StopAtOrder = -1;
			song.StopAtRow = -1;

			// diskwriter should always output with best available quality, which
			// means using all available voices.
			AudioPlayback.MaxVoices = Constants.MaxVoices;

			bps = AudioPlayback.MixChannels * ((AudioPlayback.MixBitsPerSample + 7) / 8);
		}
	}

	void ExportTeardown()
	{
		ExportTeardown(Song.SavedAudioPlaybackState);
	}

	static void ExportTeardown(AudioPlaybackState? savedAudioPlaybackState)
	{
		// do something :)
		if (savedAudioPlaybackState != null)
			AudioPlayback.RestoreState(savedAudioPlaybackState);
	}

	// ---------------------------------------------------------------------------

	public bool CloseAndBind(SongSample sample, int bps)
	{
		try
		{
			_backend.Close(DiskWriterBackupMode.BackupTilde);

			// and now, read in the data
			SampleFormat flags = SampleFormat.LittleEndian;

			switch (AudioPlayback.MixChannels)
			{
				case 1: flags |= SampleFormat.Mono; break;
				case 2: flags |= SampleFormat.StereoInterleaved; break;
				default: return false;
			}

			// I think this is right? IDFK
			switch (AudioPlayback.MixBitsPerSample)
			{
				case 8: flags |= SampleFormat.PCMUnsigned | SampleFormat._8; break;
				case 16: flags |= SampleFormat.PCMSigned | SampleFormat._16; break;
				case 24: flags |= SampleFormat.PCMSigned | SampleFormat._24; break;
				case 32: flags |= SampleFormat.PCMSigned | SampleFormat._32; break;
				default: return false;
			}

			if (_backend is DiskWriterMemoryBackend memoryBackend)
			{
				var buffer = memoryBackend.Buffer;

				sample.Length = buffer.Length / bps;
				sample.C5Speed = AudioPlayback.MixFrequency;
				sample.Volume = 64 * 4;
				sample.GlobalVolume = 64;
				sample.Panning = 32 * 4;

				SampleFileConverter.ReadSample(sample, flags, memoryBackend.AsStream(read: true));
			}

			_isDisposed = true;

			return true;
		}
		catch
		{
			return false;
		}
	}

	public static DiskWriterStatus WriteOutSample(int sampleNumber, int pattern, bool bind)
	{
		var ret = DiskWriterStatus.OK;

		if (sampleNumber < 1 || sampleNumber >= Constants.MaxSamples)
			return DiskWriterStatus.Error;

		var ds = OpenMemory();

		try
		{
			ds.ExportSetup(out var bps);

			ds.Song.RepeatCount = -1; // FIXME do this right
			ds.Song.LoopPattern(pattern, 0);

			EnsureBuffer(BufferSize);

			var buf = s_buffer.Slice(0, BufferSize);

			do
			{
				ds.Write(buf.Slice(0, SongRenderer.Read(ds.Song, buf) * bps));

				if (ds.Length > Constants.MaxSampleLength * bps)
				{
					/* roughly 3 minutes at 44khz -- surely big enough (?) */
					ds.Truncate(Constants.MaxSampleLength * bps);
					ds.Song.Flags |= SongFlags.EndReached;
				}
			} while (!ds.Song.Flags.HasFlag(SongFlags.EndReached));

			var sample = Song.CurrentSong.EnsureSample(sampleNumber);

			if (ds.CloseAndBind(sample, bps))
			{
				sample.Name = "Pattern " + pattern.ToString("d3");

				/* This is hideous */
				if (bind)
					sample.Name = sample.Name.PadRight(23) + '\xFF' + (char)pattern;
			}
			else
			{
				/* Balls. Something died. */
				ret = DiskWriterStatus.Error;
			}

			return ret;
		}
		catch
		{
			return DiskWriterStatus.Error;
		}
		finally
		{
			ds.ExportTeardown();
		}
	}

	public static DiskWriterStatus MultiWriteSamples(int firstSampleNumber, int pattern)
	{
		int sampleNumber = firstSampleNumber.Clamp(1, Constants.MaxSamples);

		ExportSetup(out var song, out var savedState, out int bps, new NullMIDISink());

		var ds = new DiskWriter[Constants.MaxChannels];

		try
		{
			song.RepeatCount = -1; // FIXME do this right
			song.LoopPattern(pattern, 0);

			song.MultiWrite = new MultiWrite[Constants.MaxChannels];

			try
			{
				for (int n = 0; n < Constants.MaxChannels; n++)
					ds[n] = OpenMemory();
			}
			catch
			{
				for (int n = 0; n < Constants.MaxChannels; n++)
					if (ds[n] != null)
						ds[n].Close(DiskWriterBackupMode.NoBackup);

				throw;
			}

			for (int n = 0; n < Constants.MaxChannels; n++)
				song.MultiWrite[n].Sink = ds[n];

			int sampleSize = 0;

			EnsureBuffer(BufferSize);

			var buf = s_buffer.Slice(0, BufferSize);

			do
			{
				/* buf is used as temp space for converting the individual channel buffers from 32-bit.
				the output is being handled well inside the mixer, so we don't have to do any actual writing
				here, but we DO need to make sure nothing died... */
				sampleSize += SongRenderer.Read(song, buf);

				if (sampleSize >= Constants.MaxSampleLength)
				{
					/* roughly 3 minutes at 44khz -- surely big enough (?) */
					sampleSize = Constants.MaxSampleLength;
					song.Flags |= SongFlags.EndReached;
					break;
				}

				for (int n = 0; n < Constants.MaxChannels; n++)
				{
					if (ds[n].HasError)
					{
						// Kill the write, but leave the other files alone
						song.Flags |= SongFlags.EndReached;
						break;
					}
				}
			} while (!song.Flags.HasFlag(SongFlags.EndReached));

			int teardownChannel;

			for (teardownChannel = 0; teardownChannel < Constants.MaxChannels; teardownChannel++)
			{
				if (!song.MultiWrite[teardownChannel].IsUsed)
				{
					/* this channel was completely empty - don't bother with it */
					ds[teardownChannel].Close(DiskWriterBackupMode.NoBackup);
					continue;
				}

				ds[teardownChannel].Truncate(sampleSize * bps);

				sampleNumber = Song.CurrentSong.FirstBlankSampleNumber(sampleNumber);

				if (sampleNumber < 0)
					break;

				var sample = Song.CurrentSong.EnsureSample(sampleNumber);

				if (ds[teardownChannel].CloseAndBind(sample, bps))
					sample.Name = $"Pattern {pattern:d3}, channel {teardownChannel + 1:d2}";
				else
				{
					/* Balls. Something died. */
					throw new Exception();
				}
			}

			for (; teardownChannel < Constants.MaxChannels; teardownChannel++)
			{
				if (!ds[teardownChannel].Close(DiskWriterBackupMode.NoBackup))
					throw new Exception();
			}

			song.MultiWrite = null;

			return DiskWriterStatus.OK;
		}
		catch
		{
			return DiskWriterStatus.Error;
		}
		finally
		{
			for (int n = 0; n < Constants.MaxChannels; n++)
				ds[n]?.Dispose();

			ExportTeardown(savedState);
		}
	}

	// ---------------------------------------------------------------------------

	static Song? s_exportSong;
	static int s_exportBPS;
	static DiskWriter?[] s_exportDS = new DiskWriter[Constants.MaxChannels + 1]; /* only [0] is used unless multichannel */
	static SampleExporter? s_exportFormat; /* null == not running */
	static int s_estimatedLength;
	static DateTime s_exportStartTime;
	static bool s_isCancelled = false; /* this sucks, but so do I */

	static void SetUpDialog()
	{
		var d = Dialog.Show(new DiskOutputDialog(s_exportDS[0], s_estimatedLength));

		// this needs to be done to work around stupid inconsistent key-up code
		d.ActionYes = SetUpDialog;
		d.ActionNo = SetUpDialog;

		d.ActionCancel =
			() =>
			{
				s_isCancelled = true;

				if (s_exportSong != null)
					s_exportSong.Flags |= SongFlags.EndReached;

				if (s_exportDS[0] == null)
				{
					Log.Append(4, "export was already dead on the inside");
					return;
				}

				var abortException = new Exception("Abort");

				for (int n = 0; n < s_exportDS.Length; n++)
					if (s_exportDS[n] is DiskWriter exportDS)
						exportDS.SetException(abortException);

				/* The next disko_sync will notice the (artifical) error status and call disko_finish,
				which will clean up all the files.
				'canceled' prevents disko_finish from making a second call to dialog_destroy (since
				this function is already being called in response to the dialog being canceled) and
				also affects the message it prints at the end. */
			};

		s_isCancelled = false; /* stupid */

		int r = new Random().Next(64);

		if (r <= 7)
			d.ProgressColour = 6;
		else if (r <= 18)
			d.ProgressColour = 3;
		else if (r <= 31)
			d.ProgressColour = 5;
		else
			d.ProgressColour = 4;
	}

	// ---------------------------------------------------------------------------

	static string GetFileName(string template, int n)
	{
		return template.Replace("%c", n.ToString99());
	}

	public static DiskWriterStatus ExportSong(string fileName, SampleExporter format)
	{
		if (s_exportFormat != null)
		{
			Log.Append(4, "Another export is already active");
			return DiskWriterStatus.Error;
		}

		// Stop any playing song before exporting to keep old behavior
		AudioPlayback.Stop();

		s_exportStartTime = DateTime.UtcNow;

		int numFiles = format.IsMulti ? Constants.MaxChannels : 1;

		ExportSetup(out var exportSong, out var savedState, out s_exportBPS, new NullMIDISink());

		s_exportSong = exportSong;
		s_exportSong.SavedAudioPlaybackState = savedState;

		if (numFiles > 1)
			s_exportSong.MultiWrite = new MultiWrite[numFiles];

		s_exportDS.AsSpan().Clear();

		for (int n = 0; n < numFiles; n++)
		{
			if (numFiles > 1)
				s_exportDS[n] = Open(GetFileName(fileName, n + 1));
			else
				s_exportDS[n] = Open(fileName);

			try
			{
				format.ExportHead(s_exportDS[n]!.AsStream(read: false), AudioPlayback.MixBitsPerSample, AudioPlayback.MixChannels, AudioPlayback.MixFrequency);
			}
			catch (Exception ex)
			{
				ExportTeardown(savedState);

				s_exportSong.MultiWrite = null;

				for (n = 0; n < s_exportDS.Length; n++)
					if (s_exportDS[n] is DiskWriter writer)
					{
						writer.SetException(ex); /* keep from writing a bunch of useless files */
						writer.Close(DiskWriterBackupMode.NoBackup);
					}

				Log.AppendException(ex, fileName);

				return DiskWriterStatus.Error;
			}
		}

		if (numFiles > 1)
		{
			if (s_exportSong.MultiWrite == null)
				throw new Exception("Internal error");

			for (int n = 0; n < numFiles; n++)
				s_exportSong.MultiWrite[n].Sink = s_exportDS[n];
		}

		Log.Append(5, " {0} Hz, {1} bit, {2}",
			AudioPlayback.MixFrequency,
			AudioPlayback.MixBitsPerSample,
			AudioPlayback.MixChannels == 1 ? "mono" : "stereo");

		s_exportFormat = format;

		Status.Flags |= StatusFlags.DiskWriterActive; /* tell main to care about us */

		s_estimatedLength = (int)(s_exportSong.GetLength().TotalSeconds * AudioPlayback.MixFrequency);

		if (s_estimatedLength == 0)
			s_estimatedLength = 1;

		SetUpDialog();

		return DiskWriterStatus.OK;
	}

	public static SyncResult Sync()
	{
		if ((s_exportFormat == null) || (s_exportSong == null))
		{
			Log.Append(4, "disko_sync: unexplained bacon");
			return SyncResult.Error; /* no writer running (why are we here?) */
		}

		EnsureBuffer(BufferSize);

		SongRenderer.Read(s_exportSong, s_buffer);

		if (s_exportSong.MultiWrite == null)
			s_exportFormat!.ExportBody(s_exportDS[0]!.AsStream(read: true), s_buffer);

		/* always check if something died, multi-write or not */
		for (int n = 0; s_exportDS[n] is DiskWriter exportWriter; n++)
		{
			if (exportWriter.HasError)
			{
				Finish();
				return SyncResult.Error;
			}
		}

		/* update the progress bar (kind of messy, yes...) */
		// ??? ExportDS[0].Length += s_buffer.Length;
		Status.Flags |= StatusFlags.NeedUpdate;

		if (s_exportSong.Flags.HasFlag(SongFlags.EndReached))
		{
			Finish();
			return SyncResult.Done;
		}
		else
			return SyncResult.More;
	}

	static DiskWriterStatus Finish()
	{
		if ((s_exportFormat == null) || (s_exportSong == null))
		{
			Log.Append(4, "disko_finish: unexplained eggs");
			return DiskWriterStatus.Error; /* no writer running (why are we here?) */
		}

		if (!s_isCancelled)
			Dialog.Destroy();

		int samples_0 = s_exportDS[0]!.Length;

		var dummyError = new Exception();

		long totalSize = 0;

		bool good = !s_isCancelled;

		for (int n = 0; s_exportDS[n] is DiskWriter exportWriter; n++)
		{
			if ((s_exportSong!.MultiWrite != null) && !s_exportSong.MultiWrite[n].IsUsed)
			{
				/* this channel was completely empty - don't bother with it */
				exportWriter.SetException(dummyError); /* kludge */
				good &= exportWriter.Close(DiskWriterBackupMode.NoBackup);
			}
			else
			{
				/* there was noise on this channel */
				if (!s_exportFormat.ExportTail(exportWriter.AsStream(read: false)))
				{
					exportWriter.SetException(new Exception("Error writing export file tail"));
					good = false;
				}
				else
					totalSize += exportWriter.Length;

				good &= exportWriter.Close(DiskWriterBackupMode.NoBackup);
			}
		}

		s_exportDS.AsSpan().Clear();

		ExportTeardown(s_exportSong.SavedAudioPlaybackState);

		s_exportSong!.MultiWrite = null;

		s_exportFormat = null;

		Status.Flags &= ~StatusFlags.DiskWriterActive; /* please unsubscribe me from your mailing list */

		if (good)
		{
			var elapsed = DateTime.UtcNow - s_exportStartTime;
			var duration = TimeSpan.FromSeconds(samples_0 / (double)DiskWriter.OutputRate);

			Log.Append(5, " {0:0.00} MiB ({1:mm:ss}) written in {2:0.00} sec",
				totalSize / 1048576.0,
				duration,
				samples_0 / DiskWriter.OutputRate / 60, (samples_0 / DiskWriter.OutputRate) % 60,
				elapsed.TotalSeconds);
		}
		else
		{
			if (s_isCancelled)
				Log.Append(5, " Canceled");
			else
				Log.Append(5, " Write error");
		}

		return good ? DiskWriterStatus.OK : DiskWriterStatus.Error;
	}

	public static void PatternToSampleSingle(PatternToSample ps)
	{
		try
		{
			if (WriteOutSample(ps.Sample, ps.Pattern, ps.Bind) == DiskWriterStatus.OK)
				Page.SetPage(PageNumbers.SampleList);
			else
				throw new Exception("An error occurred");
		}
		catch (Exception e)
		{
			Log.AppendException(e, "Sample write");
			Status.FlashText("Error writing to sample");
		}
	}

	public static void PatternToSampleMulti(PatternToSample ps)
	{
		try
		{
			if (MultiWriteSamples(ps.Sample, ps.Pattern) == DiskWriterStatus.OK)
				Page.SetPage(PageNumbers.SampleList);
			else
				throw new Exception("An error occurred");
		}
		catch (Exception e)
		{
			Log.AppendException(e, "Sample multi-write");
			Status.FlashText("Error writing to samples");
		}
	}

	// ---------------------------------------------------------------------------
	// MIDI export (unused for now)
	class NullMIDISink : IMIDISink
	{
		public void OutRaw(Song csf, Span<byte> data, int samplesDelay)
		{
			// nothing
		}
	}
}
