using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ReactorGame.Unity.P10T02
{
    public sealed class P10T02EditModeTests
    {
        [Test]
        public void ShellBuildsLandscapeSafeAreaAndNavigation()
        {
            GameObject gameObject = new GameObject("P10-T02-shell-edit-mode");
            try
            {
                Phase10ShellView shell = gameObject.AddComponent<Phase10ShellView>();
                shell.BuildVisualShell();

                Assert.That(shell.IsBuilt, Is.True);
                Assert.That(shell.ActivePage, Is.EqualTo(Phase10ShellPageV1.Dashboard));
                Assert.That(shell.NavigationButtonCount, Is.EqualTo(4));
                Assert.That(shell.ShellCanvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
                Assert.That(
                    shell.ShellCanvas.GetComponent<CanvasScaler>().referenceResolution,
                    Is.EqualTo(Phase10ShellView.LandscapeReferenceResolution));
                Assert.That(shell.SafeAreaLayout, Is.Not.Null);

                Assert.DoesNotThrow(() => shell.SafeAreaLayout.InvalidateSafeArea());
                shell.SafeAreaLayout.Apply(
                    new Rect(100.0f, 50.0f, 1720.0f, 980.0f),
                    new Vector2(1920.0f, 1080.0f));

                Assert.That(shell.SafeAreaLayout.GetComponent<RectTransform>().anchorMin.x, Is.EqualTo(100.0f / 1920.0f).Within(1e-6));
                Assert.That(shell.SafeAreaLayout.GetComponent<RectTransform>().anchorMin.y, Is.EqualTo(50.0f / 1080.0f).Within(1e-6));
                Assert.That(shell.SafeAreaLayout.GetComponent<RectTransform>().anchorMax.x, Is.EqualTo(1820.0f / 1920.0f).Within(1e-6));
                Assert.That(shell.SafeAreaLayout.GetComponent<RectTransform>().anchorMax.y, Is.EqualTo(1030.0f / 1080.0f).Within(1e-6));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void NavigationChangesOnlyTheActivePresentationPage()
        {
            GameObject gameObject = new GameObject("P10-T02-navigation-edit-mode");
            try
            {
                Phase10ShellView shell = gameObject.AddComponent<Phase10ShellView>();
                shell.BuildVisualShell();
                int pageChangedCount = 0;
                Phase10ShellPageV1 changedPage = Phase10ShellPageV1.Dashboard;
                shell.PageChanged += page =>
                {
                    pageChangedCount++;
                    changedPage = page;
                };

                Assert.That(shell.NavigateTo(Phase10ShellPageV1.Timeline), Is.True);
                Assert.That(shell.ActivePage, Is.EqualTo(Phase10ShellPageV1.Timeline));
                Assert.That(changedPage, Is.EqualTo(Phase10ShellPageV1.Timeline));
                Assert.That(pageChangedCount, Is.EqualTo(1));
                Assert.That(shell.NavigateTo(Phase10ShellPageV1.Timeline), Is.True);
                Assert.That(pageChangedCount, Is.EqualTo(1));

                Assert.That(shell.NavigateTo(Phase10ShellPageV1.Dashboard), Is.True);
                pageChangedCount = 0;
                Phase10ShellPageV1[] pages =
                {
                    Phase10ShellPageV1.Dashboard,
                    Phase10ShellPageV1.CoreMap,
                    Phase10ShellPageV1.Timeline,
                    Phase10ShellPageV1.Controls
                };
                foreach (Phase10ShellPageV1 page in pages)
                {
                    Button button;
                    Assert.That(shell.TryGetNavigationButton(page, out button), Is.True);
                    Assert.That(
                        button.GetComponent<LayoutElement>().minHeight,
                        Is.EqualTo(Phase10ShellView.MinimumTouchTargetPixels));
                    button.onClick.Invoke();
                    Assert.That(shell.ActivePage, Is.EqualTo(page));
                }

                Assert.That(pageChangedCount, Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
