using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KanbanCards.Services;
using Xunit;

namespace KanbanCards.Tests;

public class MotionCoordinatorTests
{
    [Fact]
    public void MotionCoordinator_Immediate_SetsValuesDirectly()
    {
        var thread = new Thread(() =>
        {
            var coordinator = new MotionCoordinator();
            var border = new Border();
            var translate = new TranslateTransform(10, 20);
            border.RenderTransform = translate;
            border.Opacity = 0.5;

            bool completedCalled = false;
            coordinator.AnimateX(border, translate, 150.0, TimeSpan.Zero, null, () => completedCalled = true);
            coordinator.AnimateY(border, translate, 250.0, TimeSpan.Zero, null);
            coordinator.AnimateOpacity(border, 1.0, TimeSpan.Zero, null);

            Assert.Equal(150.0, translate.X);
            Assert.Equal(250.0, translate.Y);
            Assert.Equal(1.0, border.Opacity);
            Assert.True(completedCalled);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void MotionCoordinator_CancelAndSet_OverridesAllTransforms()
    {
        var thread = new Thread(() =>
        {
            var coordinator = new MotionCoordinator();
            var border = new Border();
            var translate = new TranslateTransform(50, 50);
            border.RenderTransform = translate;
            border.Opacity = 0.2;

            coordinator.CancelAndSet(border, translate, 200, 300, 0.9);

            Assert.Equal(200.0, translate.X);
            Assert.Equal(300.0, translate.Y);
            Assert.Equal(0.9, border.Opacity);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void MotionCoordinator_WeakReferences_AllowsUIElementGarbageCollection()
    {
        var thread = new Thread(() =>
        {
            var coordinator = new MotionCoordinator();
            WeakReference weakElementRef = null!;

            new Action(() =>
            {
                var border = new Border();
                var translate = new TranslateTransform(0, 0);
                border.RenderTransform = translate;
                coordinator.AnimateX(border, translate, 100.0, TimeSpan.FromMilliseconds(50));
                weakElementRef = new WeakReference(border);
            })();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.False(weakElementRef.IsAlive, "UIElement must be garbage collected and not leaked by MotionCoordinator");
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }
}
