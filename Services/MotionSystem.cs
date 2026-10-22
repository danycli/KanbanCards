using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace KanbanCards.Services;

/// <summary>
/// Centralized motion design system for KanbanCards.
/// Provides unified Apple-grade easing functions, interruptible transform animations,
/// and timing scaled by user preferences.
/// </summary>
public static class MotionSystem
{
    // Pre-allocated and frozen easing functions for maximum performance and zero GC allocations
    public static readonly CubicEase CubicOut = new() { EasingMode = EasingMode.EaseOut };
    public static readonly CubicEase CubicInOut = new() { EasingMode = EasingMode.EaseInOut };
    public static readonly CubicEase CubicIn = new() { EasingMode = EasingMode.EaseIn };
    public static readonly QuadraticEase QuadOut = new() { EasingMode = EasingMode.EaseOut };
    public static readonly QuadraticEase QuadIn = new() { EasingMode = EasingMode.EaseIn };

    static MotionSystem()
    {
        CubicOut.Freeze();
        CubicInOut.Freeze();
        CubicIn.Freeze();
        QuadOut.Freeze();
        QuadIn.Freeze();
    }

    /// <summary>
    /// Scales a base duration according to the user's animation settings.
    /// </summary>
    public static TimeSpan GetDuration(TimeSpan baseDuration, AppSettings? settings)
    {
        if (settings == null || !settings.EnableAnimations)
            return TimeSpan.Zero;

        // AnimationSpeed ranges 0 to 100:
        // 50 is default baseline (1.0x factor).
        // 0 is gentle motion (1.45x duration).
        // 100 is crisp/instant motion (0.35x duration).
        double factor = Math.Clamp(1.45 - (settings.AnimationSpeed / 100.0) * 1.1, 0.2, 1.8);
        return TimeSpan.FromMilliseconds(baseDuration.TotalMilliseconds * factor);
    }

    // Standard motion durations
    public static TimeSpan HoverExpandDuration(AppSettings? settings) =>
        GetDuration(TimeSpan.FromMilliseconds(220), settings);

    public static TimeSpan HoverCollapseDuration(AppSettings settings) =>
        GetDuration(TimeSpan.FromMilliseconds(200), settings);

    public static TimeSpan ScrollShiftDuration(AppSettings settings) =>
        GetDuration(TimeSpan.FromMilliseconds(160), settings);

    public static TimeSpan CardRepositionDuration(AppSettings settings) =>
        GetDuration(TimeSpan.FromMilliseconds(220), settings);

    public static TimeSpan CardAppearanceDuration(AppSettings settings) =>
        GetDuration(TimeSpan.FromMilliseconds(180), settings);

    public static TimeSpan CardRemovalDuration(AppSettings settings) =>
        GetDuration(TimeSpan.FromMilliseconds(160), settings);

    public static TimeSpan HoverScaleDuration(AppSettings settings) =>
        GetDuration(TimeSpan.FromMilliseconds(130), settings);

    /// <summary>
    /// Calculates the target horizontal translation (X) for a note card at slotIndex.
    /// In collapsed state, cards form a subtle staircase peeking from the edge.
    /// In expanded state, cards expand out with an elegant fanned staircase depth.
    /// </summary>
    public static double GetCardTargetX(int slotIndex, bool isExpanded, bool isLeft)
    {
        // Standard card width is 280px
        if (isExpanded)
        {
            // Slot 0 (top/newest) is fully visible (280px).
            // Successive slots are stepped by 16px to create the fanned staircase.
            double hiddenOffset = Math.Max(0, slotIndex * 16.0);
            return isLeft ? -hiddenOffset : hiddenOffset;
        }
        else
        {
            // Slot 0 peeks 48px from the screen edge.
            // Successive slots peek 4px less, down to a minimum peek of 8px.
            double peek = Math.Max(8.0, 48.0 - (slotIndex * 4.0));
            double hiddenOffset = 280.0 - peek;
            return isLeft ? -hiddenOffset : hiddenOffset;
        }
    }

    /// <summary>
    /// Calculates target horizontal translation for the Add Note button,
    /// positioned directly below the existing cards.
    /// </summary>
    public static double GetAddButtonTargetX(int cardCount, bool isExpanded, bool isLeft)
    {
        return GetCardTargetX(cardCount, isExpanded, isLeft);
    }

    /// <summary>
    /// Smoothly animates a Double property using HandoffBehavior.SnapshotAndReplace
    /// to ensure completely interruptible transitions without snapping.
    /// If duration is TimeSpan.Zero, applies immediately.
    /// </summary>
    public static void AnimateDouble(
        IAnimatable animatable,
        DependencyProperty property,
        double toValue,
        TimeSpan duration,
        IEasingFunction? easing = null,
        EventHandler? completed = null)
    {
        if (duration == TimeSpan.Zero)
        {
            animatable.BeginAnimation(property, null);
            if (animatable is DependencyObject dobj)
            {
                dobj.SetValue(property, toValue);
            }
            completed?.Invoke(animatable, EventArgs.Empty);
            return;
        }

        var anim = new DoubleAnimation
        {
            To = toValue,
            Duration = duration,
            EasingFunction = easing ?? CubicOut
        };

        if (completed != null)
        {
            anim.Completed += completed;
        }

        animatable.BeginAnimation(property, anim, HandoffBehavior.SnapshotAndReplace);
    }
}
