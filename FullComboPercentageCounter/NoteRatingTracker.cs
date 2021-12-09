﻿using System;
using System.Collections.Generic;
using Zenject;

// I copied a part of PikminBloom's homework and changed a few things so it isn't obvious.

namespace FullComboPercentageCounter
{
	public class NoteRatingTracker : IInitializable, IDisposable, ISaberSwingRatingCounterDidChangeReceiver, ISaberSwingRatingCounterDidFinishReceiver
	{
		public event EventHandler<NoteRatingUpdateEventArgs> OnRatingAdded;
		public event EventHandler<NoteRatingUpdateEventArgs> OnRatingFinished;
		public event EventHandler<NoteMissedEventArgs> OnNoteMissed;
		public event Action OnComboBreak;

		private readonly ScoreController scoreController;

		private Dictionary<NoteData, NoteRating> noteRatings;
		private Dictionary<ISaberSwingRatingCounter, NoteCutInfo> swingCounterCutInfo;
		private Dictionary<NoteCutInfo, NoteData> noteCutInfoData;

		private int noteCount;

		public NoteRatingTracker(ScoreController scoreController)
		{
			this.scoreController = scoreController;
		}

		public void Initialize()
		{
			scoreController.noteWasMissedEvent += ScoreController_noteWasMissedEvent;
			scoreController.noteWasCutEvent += ScoreController_noteWasCutEvent;
			scoreController.comboBreakingEventHappenedEvent += ScoreController_OnComboBreakingEvent;

			noteRatings = new Dictionary<NoteData, NoteRating>();
			swingCounterCutInfo = new Dictionary<ISaberSwingRatingCounter, NoteCutInfo>();
			noteCutInfoData = new Dictionary<NoteCutInfo, NoteData>();

			noteCount = 0;
		}

		public void Dispose()
		{
			scoreController.noteWasMissedEvent -= ScoreController_noteWasMissedEvent;
			scoreController.noteWasCutEvent -= ScoreController_noteWasCutEvent;
			scoreController.comboBreakingEventHappenedEvent -= ScoreController_OnComboBreakingEvent;
		}

		private void ScoreController_noteWasMissedEvent(NoteData noteData, int _)
		{
			noteCount++;
			InvokeNoteMissed(noteData, noteCount);
		}

		private void ScoreController_noteWasCutEvent(NoteData noteData, in NoteCutInfo noteCutInfo, int multiplier)
		{
			noteCount++;
			if (noteData.colorType != ColorType.None)
			{
				if (noteCutInfo.allIsOK)
				{
					swingCounterCutInfo.Add(noteCutInfo.swingRatingCounter, noteCutInfo);
					noteCutInfoData.Add(noteCutInfo, noteData);
					noteCutInfo.swingRatingCounter.RegisterDidChangeReceiver(this);
					noteCutInfo.swingRatingCounter.RegisterDidFinishReceiver(this);

					int beforeCutRawScore, afterCutRawScore, accRawScore;
					ScoreModel.RawScoreWithoutMultiplier(noteCutInfo.swingRatingCounter, noteCutInfo.cutDistanceToCenter, out beforeCutRawScore, out afterCutRawScore, out accRawScore);
					NoteRating noteRating = new NoteRating(beforeCutRawScore, afterCutRawScore, accRawScore, multiplier, noteCount);
					noteRatings.Add(noteData, noteRating);

					InvokeRatingAdded(noteData, noteRating);
				}
				else
				{
					InvokeNoteMissed(noteData, noteCount);
				}
			}
		}

		private void ScoreController_OnComboBreakingEvent()
		{
			OnComboBreak.Invoke();
		}

		public void HandleSaberSwingRatingCounterDidChange(ISaberSwingRatingCounter saberSwingRatingCounter, float rating)
		{
			NoteCutInfo noteCutInfo;
			if (swingCounterCutInfo.TryGetValue(saberSwingRatingCounter, out noteCutInfo))
			{
				NoteData noteData;
				if (noteCutInfoData.TryGetValue(noteCutInfo, out noteData))
				{
					int beforeCutRawScore, afterCutRawScore, accRawScore;
					ScoreModel.RawScoreWithoutMultiplier(saberSwingRatingCounter, noteCutInfo.cutDistanceToCenter, out beforeCutRawScore, out afterCutRawScore, out accRawScore);

					noteRatings[noteData].UpdateRating(beforeCutRawScore, afterCutRawScore, accRawScore);
				}
				else
					Plugin.Log.Error("noteRatingTracker, HandleSaberSwingRatingCounterDidChange : Failed to get NoteData from noteCutInfoData!");
			}
			else
				Plugin.Log.Error("noteRatingTracker, HandleSaberSwingRatingCounterDidChange : Failed to get NoteCutInfo from swingCounterCutInfo!");
		}

		public void HandleSaberSwingRatingCounterDidFinish(ISaberSwingRatingCounter saberSwingRatingCounter)
		{
			NoteCutInfo noteCutInfo;
			if (swingCounterCutInfo.TryGetValue(saberSwingRatingCounter, out noteCutInfo))
			{
				NoteData noteData;
				if (noteCutInfoData.TryGetValue(noteCutInfo, out noteData))
				{
					NoteRating noteRating = noteRatings[noteData];
					InvokeRatingFinished(noteData, noteRating);
					noteRatings.Remove(noteData);
				}
				else
					Plugin.Log.Error("noteRatingTracker, HandleSaberSwingRatingCounterDidFinish : Failed to get NoteData from noteCutInfoData!");

				swingCounterCutInfo.Remove(saberSwingRatingCounter);
			}
			else
				Plugin.Log.Error("noteRatingTracker, HandleSaberSwingRatingCounterDidFinish : Failed to get NoteCutInfo from swingCounterCutInfo!");

			saberSwingRatingCounter.UnregisterDidChangeReceiver(this);
			saberSwingRatingCounter.UnregisterDidFinishReceiver(this);
		}

		protected virtual void InvokeRatingAdded(NoteData noteData, NoteRating noteRating)
		{
			EventHandler<NoteRatingUpdateEventArgs> handler = OnRatingAdded;
			if (handler != null)
			{
				NoteRatingUpdateEventArgs noteRatingUpdateEventArgs = new NoteRatingUpdateEventArgs();
				noteRatingUpdateEventArgs.NoteData = noteData;
				noteRatingUpdateEventArgs.NoteRating = noteRating;

				handler(this, noteRatingUpdateEventArgs);
			}
		}
		protected virtual void InvokeRatingFinished(NoteData noteData, NoteRating noteRating)
		{
			EventHandler<NoteRatingUpdateEventArgs> handler = OnRatingFinished;
			if (handler != null)
			{
				NoteRatingUpdateEventArgs eventArgs = new NoteRatingUpdateEventArgs();
				eventArgs.NoteData = noteData;
				eventArgs.NoteRating = noteRating;

				handler(this, eventArgs);
			}
		}
		protected virtual void InvokeNoteMissed(NoteData noteData, int noteCount)
		{
			EventHandler<NoteMissedEventArgs> handler = OnNoteMissed;
			if (handler != null) 
			{
				NoteMissedEventArgs eventArgs = new NoteMissedEventArgs();
				eventArgs.NoteData = noteData;
				eventArgs.NoteCount = noteCount;

				handler(this, eventArgs);
			}
		}
	}

	public class NoteRatingUpdateEventArgs : EventArgs
	{
		public NoteData NoteData { get; set; }
		public NoteRating NoteRating { get; set; }
	}

	public class NoteMissedEventArgs : EventArgs
	{
		public NoteData NoteData { get; set; }
		public int NoteCount { get; set; }
	}

	public class NoteRating
	{
		public int beforeCut, afterCut, acc, multiplier, noteCount;

		public NoteRating(int beforeCutRaw, int afterCutRaw, int accRaw, int multiplier, int noteCount)
		{
			this.beforeCut = beforeCutRaw;
			this.afterCut = afterCutRaw;
			this.acc = accRaw;
			this.multiplier = multiplier;
			this.noteCount = noteCount;
		}

		public void UpdateRating(int beforeCutRaw, int afterCutRaw, int accRaw)
		{
			beforeCut = beforeCutRaw;
			afterCut = afterCutRaw;
			acc = accRaw;
		}
	}
}

