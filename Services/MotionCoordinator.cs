using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace KanbanCards.Services;

/// <summary>
/// Centralized interruptible motion coordinator for KanbanCards.
/// Ensures low-latency input handling, deterministic target states,
/// safe retargeting without visual snapping, and token-based protection
/// against stale animation completion callbacks.
/// </summary>
public class MotionCoordinator
{
    private class TokenHolder { public long Value; }
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<UIElement, TokenHolder> _activeTokens = new();

    private long NextToken(UIElement element)
    {
        var holder = _activeTokens.GetOrCreateValue(element);
        return ++holder.Value;
    }

    /// <summary>
    /// Smoothly animates TranslateTransform.X from its current presentation position
    /// to targetX. If duration is zero or element is already at targetX, applies immediately.
    /// </summary>
    public void AnimateX(
        UIElement element,
        TranslateTransform transform,
        double targetX,
        TimeSpan duration,
        IEasingFunction? easing = null,
        Action? completed = null)
    {
        if (duration <= TimeSpan.Zero)
        {
            transform.BeginAnimation(TranslateTransform.XProperty, null);
            transform.X = targetX;
            completed?.Invoke();
            return;
        }

        double currentX = transform.X;
        if (Math.Abs(currentX - targetX) < 0.1)
        {
            transform.BeginAnimation(TranslateTransform.XProperty, null);
            transform.X = targetX;
            completed?.Invoke();
            return;
        }

        long token = NextToken(element);

        var anim = new DoubleAnimation
        {
            From = currentX,
            To = targetX,
            Duration = duration,
            EasingFunction = easing ?? MotionSystem.CubicOut
        };

        if (completed != null)
        {
            anim.Completed += (_, _) =>
            {
                if (_activeTokens.TryGetValue(element, out var holder) && holder.Value == token)
                {
                    completed();
                }
            };
        }

        transform.BeginAnimation(TranslateTransform.XProperty, anim, HandoffBehavior.SnapshotAndReplace);
    }

    /// <summary>
    /// Smoothly animates TranslateTransform.Y from its current presentation position
    /// to targetY. If duration is zero or element is already at targetY, applies immediately.
    /// </summary>
    public void AnimateY(
        UIElement element,
        TranslateTransform transform,
        double targetY,
        TimeSpan duration,
        IEasingFunction? easing = null,
        Action? completed = null)
    {
        if (duration <= TimeSpan.Zero)
        {
            transform.BeginAnimation(TranslateTransform.YProperty, null);
            transform.Y = targetY;
            completed?.Invoke();
            return;
        }

        double currentY = transform.Y;
        if (Math.Abs(currentY - targetY) < 0.1)
        {
            transform.BeginAnimation(TranslateTransform.YProperty, null);
            transform.Y = targetY;
            completed?.Invoke();
            return;
        }

        long token = NextToken(element);

        var anim = new DoubleAnimation
        {
            From = currentY,
            To = targetY,
            Duration = duration,
            EasingFunction = easing ?? MotionSystem.CubicOut
        };

        if (completed != null)
        {
            anim.Completed += (_, _) =>
            {
                if (_activeTokens.TryGetValue(element, out var holder) && holder.Value == token)
                {
                    completed();
                }
            };
        }

        transform.BeginAnimation(TranslateTransform.YProperty, anim, HandoffBehavior.SnapshotAndReplace);
    }

    /// <summary>
    /// Smoothly animates UIElement.Opacity from its current presentation value to targetOpacity.
    /// </summary>
    public void AnimateOpacity(
        UIElement element,
        double targetOpacity,
        TimeSpan duration,
        IEasingFunction? easing = null,
        Action? completed = null)
    {
        if (duration <= TimeSpan.Zero)
        {
            element.BeginAnimation(UIElement.OpacityProperty, null);
            element.Opacity = targetOpacity;
            completed?.Invoke();
            return;
        }

        double currentOpacity = element.Opacity;
        if (Math.Abs(currentOpacity - targetOpacity) < 0.01)
        {
            element.BeginAnimation(UIElement.OpacityProperty, null);
            element.Opacity = targetOpacity;
            completed?.Invoke();
            return;
        }

        long token = NextToken(element);

        var anim = new DoubleAnimation
        {
            From = currentOpacity,
            To = targetOpacity,
            Duration = duration,
            EasingFunction = easing ?? MotionSystem.CubicOut
        };

        if (completed != null)
        {
            anim.Completed += (_, _) =>
            {
                if (_activeTokens.TryGetValue(element, out var holder) && holder.Value == token)
                {
                    completed();
                }
            };
        }

        element.BeginAnimation(UIElement.OpacityProperty, anim, HandoffBehavior.SnapshotAndReplace);
    }

    /// <summary>
    /// Smoothly animates ScaleTransform.ScaleX and ScaleY concurrently.
    /// </summary>
    public void AnimateScale(
        UIElement element,
        ScaleTransform transform,
        double targetScale,
        TimeSpan duration,
        IEasingFunction? easing = null)
    {
        if (duration <= TimeSpan.Zero)
        {
            transform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            transform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            transform.ScaleX = targetScale;
            transform.ScaleY = targetScale;
            return;
        }

        var animX = new DoubleAnimation
        {
            From = transform.ScaleX,
            To = targetScale,
            Duration = duration,
            EasingFunction = easing ?? MotionSystem.QuadOut
        };

        var animY = new DoubleAnimation
        {
            From = transform.ScaleY,
            To = targetScale,
            Duration = duration,
            EasingFunction = easing ?? MotionSystem.QuadOut
        };

        transform.BeginAnimation(ScaleTransform.ScaleXProperty, animX, HandoffBehavior.SnapshotAndReplace);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, animY, HandoffBehavior.SnapshotAndReplace);
    }

    /// <summary>
    /// Cancels any active animations on the element and sets target properties immediately.
    /// </summary>
    public void CancelAndSet(UIElement element, TranslateTransform transform, double x, double y, double opacity)
    {
        NextToken(element);
        transform.BeginAnimation(TranslateTransform.XProperty, null);
        transform.BeginAnimation(TranslateTransform.YProperty, null);
        element.BeginAnimation(UIElement.OpacityProperty, null);

        transform.X = x;
        transform.Y = y;
        element.Opacity = opacity;
    }

    /// <summary>
    /// Cleans up tracking for an element that was removed from the visual tree.
    /// </summary>
    public void Cleanup(UIElement element)
    {
        _activeTokens.Remove(element);
        if (element.RenderTransform is TransformGroup group)
        {
            foreach (var child in group.Children)
            {
                if (child is TranslateTransform tt)
                {
                    tt.BeginAnimation(TranslateTransform.XProperty, null);
                    tt.BeginAnimation(TranslateTransform.YProperty, null);
                }
                else if (child is ScaleTransform st)
                {
                    st.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                    st.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                }
            }
        }
        else if (element.RenderTransform is TranslateTransform tt)
        {
            tt.BeginAnimation(TranslateTransform.XProperty, null);
            tt.BeginAnimation(TranslateTransform.YProperty, null);
        }
        element.BeginAnimation(UIElement.OpacityProperty, null);
    }
}
