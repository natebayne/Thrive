﻿using System;
using System.Collections.Generic;
using Godot;

/// <summary>
///   Manages the screen transitions, usually used for when
///   switching scenes. This is autoloaded
/// </summary>
public class TransitionManager : ControlWithInput
{
    private static TransitionManager? instance;

    private readonly PackedScene screenFadeScene;
    private readonly PackedScene cutsceneScene;

    /// <summary>
    ///   A queue of running and pending transition sequences.
    /// </summary>
    private readonly Queue<Sequence> queuedSequences = new();

    private TransitionManager()
    {
        instance = this;

        screenFadeScene = GD.Load<PackedScene>("res://src/gui_common/ScreenFade.tscn");
        cutsceneScene = GD.Load<PackedScene>("res://src/gui_common/Cutscene.tscn");
    }

    [Signal]
    public delegate void QueuedTransitionsFinished();

    public static TransitionManager Instance => instance ?? throw new InstanceNotLoadedYetException();

    public bool HasQueuedTransitions => queuedSequences.Count > 0;

    private ScreenFade.FadeType? LastFadedType { get; set; }

    /// <summary>
    ///   Helper method for creating a screen fade.
    /// </summary>
    /// <param name="type">The type of fade to transition to</param>
    /// <param name="fadeDuration">How long the fade lasts</param>
    public ScreenFade CreateScreenFade(ScreenFade.FadeType type, float fadeDuration)
    {
        // Instantiate scene
        var screenFade = (ScreenFade)screenFadeScene.Instance();

        screenFade.CurrentFadeType = type;
        screenFade.FadeDuration = fadeDuration;

        return screenFade;
    }

    /// <summary>
    ///   Helper method for creating a video cutscene.
    /// </summary>
    /// <param name="path">The video file to play</param>
    /// <param name="volume">The video player's volume in linear value</param>
    public Cutscene CreateCutscene(string path, float volume = 1.0f)
    {
        // Instantiate scene
        var cutscene = (Cutscene)cutsceneScene.Instance();

        cutscene.Volume = volume;
        cutscene.Stream = GD.Load<VideoStream>(path);

        return cutscene;
    }

    public override void _Process(float delta)
    {
        if (queuedSequences.Count > 0)
        {
            var sequence = queuedSequences.Peek();

            sequence.Process();

            if (sequence.Finished)
            {
                queuedSequences.Dequeue();
                SaveHelper.AllowQuickSavingAndLoading = !HasQueuedTransitions;
            }
        }
    }

    [RunOnKeyDown("ui_cancel", OnlyUnhandled = false)]
    public bool CancelTransitionPressed()
    {
        if (!HasQueuedTransitions)
            return false;

        CancelSequences();
        return true;
    }

    /// <summary>
    ///   Enqueues a new <see cref="Sequence"/> from the given list of transitions. This defaults to skipping any
    ///   previous sequences, can be changed with <paramref name="skipPrevious"/> parameter.
    ///   Invokes the specified action when the sequence is finished.
    /// </summary>
    /// <param name="transitions">Order of transitions</param>
    /// <param name="onFinishedCallback">The action to invoke when the sequence is finished</param>
    /// <param name="skippable">If true, the sequence can be skipped</param>
    /// <param name="skipPrevious">If false, will not skip any previously added sequences</param>
    public void AddSequence(List<ITransition> transitions, Action? onFinishedCallback = null, bool skippable = true,
        bool skipPrevious = true)
    {
        if (transitions.Count <= 0)
        {
            GD.PrintErr("The given list of transitions is empty, can't add sequence to the queue");
            return;
        }

        if (skipPrevious)
        {
            foreach (var entry in queuedSequences)
                entry.Skip();
        }

        foreach (var transition in transitions)
        {
            if (transition is Node node)
                AddChild(node);
        }

        var sequence = new Sequence(transitions, onFinishedCallback) { Skippable = skippable };
        queuedSequences.Enqueue(sequence);

        SaveHelper.AllowQuickSavingAndLoading = false;
    }

    /// <summary>
    ///   Enqueues a new <see cref="Sequence"/> from the given transition. This defaults to skipping any
    ///   previous sequences, can be changed with <paramref name="skipPrevious"/> parameter.
    ///   Invokes the specified action when the sequence is finished.
    /// </summary>
    /// <param name="transition">The specified transition</param>
    /// <param name="onFinishedCallback">The action to invoke when the sequence is finished</param>
    /// <param name="skippable">If true, the sequence can be skipped</param>
    /// <param name="skipPrevious">If false, will not skip any previously added sequences</param>
    public void AddSequence(ITransition transition, Action? onFinishedCallback = null, bool skippable = true,
        bool skipPrevious = true)
    {
        AddSequence(new List<ITransition> { transition }, onFinishedCallback, skippable, skipPrevious);
    }

    /// <summary>
    ///   Enqueues a new <see cref="Sequence"/> from the given type of screen fade.
    ///   Invokes the specified action when the sequence is finished.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     A single screen fade is commonly used which is why a dedicated overload method is
    ///     supported to make it easier to invoke with less verbosity.
    ///   </para>
    /// </remarks>
    public void AddSequence(ScreenFade.FadeType fadeType, float duration, Action? onFinishedCallback = null,
        bool skippable = true, bool skipPrevious = true)
    {
        AddSequence(CreateScreenFade(fadeType, duration), onFinishedCallback, skippable, skipPrevious);
    }

    /// <summary>
    ///   Skips all the running and remaining transition sequences.
    /// </summary>
    private void CancelSequences()
    {
        foreach (var sequence in queuedSequences)
            sequence.Skip();
    }

    /// <summary>
    ///   A sequence of <see cref="ITransition"/>s. Has its own on finished callback.
    /// </summary>
    public class Sequence
    {
        private Queue<ITransition> queuedTransitions = new();
        private Action? onFinishedCallback;

        public Sequence(List<ITransition> transitions, Action? onFinishedCallback)
        {
            foreach (var transition in transitions)
            {
                queuedTransitions.Enqueue(transition);
            }

            this.onFinishedCallback = onFinishedCallback;
        }

        public bool Skippable { get; set; }

        public bool Finished { get; private set; }

        /// <summary>
        ///   If true this means this sequence is in the state of executing/has executed a transition.
        /// </summary>
        public bool Running { get; private set; }

        public void Skip()
        {
            if (!Skippable)
                return;

            foreach (var transition in queuedTransitions)
                transition.Skip();
        }

        /// <summary>
        ///   Awaits for one transition to finish before moving on to the next and clears the previous.
        /// </summary>
        public void Process()
        {
            if (queuedTransitions.Count <= 0)
                return;

            if (!Running)
            {
                Running = true;
                StartNext();
            }

            if (queuedTransitions.Peek().Finished)
            {
                var previous = queuedTransitions.Dequeue();

                // Defer call to avoid possible "flickers"
                Invoke.Instance.Queue(() => previous.Clear());

                if (previous is ScreenFade fade)
                    Instance.LastFadedType = fade.CurrentFadeType;

                StartNext();
            }
        }

        /// <summary>
        ///   Starts the front-most transition on the queue.
        /// </summary>
        private void StartNext()
        {
            if (queuedTransitions.Count > 0)
            {
                var front = queuedTransitions.Peek();

                // Hard disallow incorrect fade order
                if (front is ScreenFade fade && fade.CurrentFadeType == Instance.LastFadedType)
                {
                    front.Skip();
                    return;
                }

                Instance.LastFadedType = null;
                front.Begin();
                return;
            }

            Instance.LastFadedType = null;

            // Assume all transitions are finished if the queue is empty.
            Finished = true;
            Running = false;
            onFinishedCallback?.Invoke();
        }
    }
}
