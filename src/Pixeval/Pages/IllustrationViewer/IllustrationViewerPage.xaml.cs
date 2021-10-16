﻿using System;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
using Microsoft.Toolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Media.Animation;
using Pixeval.Messages;
using Pixeval.Misc;
using Pixeval.Options;
using Pixeval.Popups;
using Pixeval.Util;
using Pixeval.Util.IO;
using Pixeval.Util.UI;
using Pixeval.Utilities;
using Pixeval.ViewModel;

namespace Pixeval.Pages.IllustrationViewer
{
    public sealed partial class IllustrationViewerPage : IGoBack
    {
        private IllustrationViewerPageViewModel _viewModel = null!;

        public IllustrationViewerPage()
        {
            InitializeComponent();
            var dataTransferManager = UIHelper.GetDataTransferManager();
            dataTransferManager.DataRequested += OnDataTransferManagerOnDataRequested;
        }

        private void IllustrationViewerPage_OnLoaded(object sender, RoutedEventArgs e)
        {
            SidePanelShadow.Receivers.Add(IllustrationPresenterDockPanel);
            PopupShadow.Receivers.Add(IllustrationInfoAndCommentsSplitView);
        }

        public override void OnPageDeactivated(NavigatingCancelEventArgs e)
        {
            foreach (var imageViewerPageViewModel in _viewModel.ImageViewerPageViewModels)
            {
                imageViewerPageViewModel.ImageLoadingCancellationHandle.Cancel();
            }
            _viewModel.Dispose();
            WeakReferenceMessenger.Default.UnregisterAll(this);
        }

        public override void OnPageActivated(NavigationEventArgs e)
        {
            if (ConnectedAnimationService.GetForCurrentView().GetAnimation("ForwardConnectedAnimation") is { } animation)
            {
                animation.Configuration = new DirectConnectedAnimationConfiguration();
                animation.TryStart(IllustrationImageShowcaseFrame);
            }

            _viewModel = (IllustrationViewerPageViewModel) e.Parameter;
            _illustrationInfo = new NavigationViewTag(typeof(IllustrationInfoPage), _viewModel);
            _comments = new NavigationViewTag(typeof(CommentsPage), (App.AppViewModel.MakoClient.IllustrationComments(_viewModel.IllustrationId).Where(c => c is not null), _viewModel.IllustrationId)); // TODO

            IllustrationImageShowcaseFrame.Navigate(typeof(ImageViewerPage), _viewModel.Current);

            WeakReferenceMessenger.Default.Send(new MainPageFrameSetConnectedAnimationTargetMessage(_viewModel.IllustrationGrid?.GetItemContainer(_viewModel.IllustrationViewModelInTheGridView!) ?? App.AppViewModel.AppWindowRootFrame));
            WeakReferenceMessenger.Default.Register<IllustrationViewerPage, CommentRepliesHyperlinkButtonTappedMessage>(this, CommentRepliesHyperlinkButtonTapped);
        }

        private static void CommentRepliesHyperlinkButtonTapped(IllustrationViewerPage recipient, CommentRepliesHyperlinkButtonTappedMessage message)
        {
            var commentRepliesBlock = new CommentRepliesBlock(new CommentRepliesBlockViewModel(message.Sender!.GetDataContext<CommentBlockViewModel>()));
            commentRepliesBlock.CloseButtonTapped += recipient.CommentRepliesBlock_OnCloseButtonTapped;
            recipient._commentRepliesPopup = PopupManager.CreatePopup(commentRepliesBlock, widthMargin: 200, maxWidth: 1500, minWidth: 400, maxHeight: 1200, closing: (_, _) => recipient._commentRepliesPopup = null);
            PopupManager.ShowPopup(recipient._commentRepliesPopup);
        }

        private AppPopup? _commentRepliesPopup;

        private void CommentRepliesBlock_OnCloseButtonTapped(object? sender, TappedRoutedEventArgs e)
        {
            if (_commentRepliesPopup is not null)
            {
                PopupManager.ClosePopup(_commentRepliesPopup);
            }
        }

        private async void OnDataTransferManagerOnDataRequested(DataTransferManager _, DataRequestedEventArgs args)
        {
            // Remarks: all the illustrations in _viewModels only differ in different image sources
            var vm = _viewModel.Current.IllustrationViewModel;
            if (_viewModel.Current.LoadingOriginalSourceTask is not {IsCompletedSuccessfully: true})
            {
                return;
            }

            var request = args.Request;
            var deferral = request.GetDeferral();
            var props = request.Data.Properties;
            var webLink = MakoHelper.GetIllustrationWebUri(vm.Id);

            props.Title = IllustrationViewerPageResources.ShareTitleFormatted.Format(vm.Id);
            props.Description = vm.Illustration.Title;
            props.Square30x30Logo = RandomAccessStreamReference.CreateFromStream(await AppContext.GetAssetStreamAsync("Images/logo44x44.ico"));

            var thumbnailStream = await _viewModel.Current.IllustrationViewModel.GetThumbnail(ThumbnailUrlOption.SquareMedium);
            var file = await AppContext.CreateTemporaryFileWithRandomNameAsync(_viewModel.IsUgoira ? "gif" : "png");

            if (_viewModel.Current.OriginalImageStream is { } stream)
            {
                await stream.SaveToFile(file);

                props.Thumbnail = RandomAccessStreamReference.CreateFromStream(thumbnailStream);

                request.Data.SetStorageItems(Enumerates.ArrayOf(file), true);
                request.Data.SetWebLink(webLink);
                request.Data.SetApplicationLink(AppContext.GenerateAppLinkToIllustration(vm.Id));
            }

            deferral.Complete();
        }

        private void NextImage()
        {
            IllustrationImageShowcaseFrame.Navigate(typeof(ImageViewerPage), _viewModel.Next(), new SlideNavigationTransitionInfo
            {
                Effect = SlideNavigationTransitionEffect.FromRight
            });
        }

        private void PrevImage()
        {
            IllustrationImageShowcaseFrame.Navigate(typeof(ImageViewerPage), _viewModel.Prev(), new SlideNavigationTransitionInfo
            {
                Effect = SlideNavigationTransitionEffect.FromLeft
            });
        }

        private void NextIllustration()
        {
            var illustrationViewModel = (IllustrationViewModel) _viewModel.ContainerGridViewModel!.IllustrationsView[_viewModel.IllustrationIndex!.Value + 1];
            var viewModel = illustrationViewModel.GetMangaIllustrationViewModels().ToArray();

            App.AppViewModel.RootFrameNavigate(typeof(IllustrationViewerPage), new IllustrationViewerPageViewModel(_viewModel.IllustrationGrid!, viewModel), new SlideNavigationTransitionInfo
            {
                Effect = SlideNavigationTransitionEffect.FromRight
            });
        }

        private void PrevIllustration()
        {
            var illustrationViewModel = (IllustrationViewModel) _viewModel.ContainerGridViewModel!.IllustrationsView[_viewModel.IllustrationIndex!.Value - 1];
            var viewModel = illustrationViewModel.GetMangaIllustrationViewModels().ToArray();

            App.AppViewModel.RootFrameNavigate(typeof(IllustrationViewerPage), new IllustrationViewerPageViewModel(_viewModel.IllustrationGrid!, viewModel), new SlideNavigationTransitionInfo
            {
                Effect = SlideNavigationTransitionEffect.FromLeft
            });
        }

        private void NextImageAppBarButton_OnTapped(object sender, TappedRoutedEventArgs e)
        {
            NextImage();
        }

        private void PrevImageAppBarButton_OnTapped(object sender, TappedRoutedEventArgs e)
        {
            PrevImage();
        }

        private void NextIllustrationAppBarButton_OnTapped(object sender, TappedRoutedEventArgs e)
        {
            NextIllustration();
        }

        private void PrevIllustrationAppBarButton_OnTapped(object sender, TappedRoutedEventArgs e)
        {
            PrevIllustration();
        }

        private void BackButton_OnTapped(object sender, TappedRoutedEventArgs e)
        {
            GoBack();
        }

        private void GenerateLinkToThisPageButtonTeachingTip_OnActionButtonClick(TeachingTip sender, object args)
        {
            _viewModel.IsGenerateLinkTeachingTipOpen = false;
            App.AppViewModel.AppSetting.DisplayTeachingTipWhenGeneratingAppLink = false;
        }

        private void IllustrationInfoAndCommentsNavigationView_OnBackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            IllustrationInfoAndCommentsSplitView.IsPaneOpen = false;
        }

        private void IllustrationInfoAndCommentsNavigationView_OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (sender.SelectedItem is NavigationViewItem { Tag: NavigationViewTag tag })
            {
                IllustrationInfoAndCommentsFrame.Navigate(tag.NavigateTo, tag.Parameter, new SlideNavigationTransitionInfo
                {
                    Effect = tag switch
                    { 
                        var x when x == _illustrationInfo => SlideNavigationTransitionEffect.FromLeft,
                        var x when x == _comments => SlideNavigationTransitionEffect.FromRight,
                        _ => throw new ArgumentOutOfRangeException()
                    }
                });
            }
        }

        public void GoBack()
        {
            ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("ForwardConnectedAnimation", IllustrationImageShowcaseFrame);
            WeakReferenceMessenger.Default.Send(new NavigatingBackToMainPageMessage(_viewModel.IllustrationViewModelInTheGridView));

            App.AppViewModel.AppWindowRootFrame.BackStack.RemoveAll(entry => entry.SourcePageType == typeof(IllustrationViewerPage));
            if (App.AppViewModel.AppWindowRootFrame.CanGoBack)
            {
                App.AppViewModel.AppWindowRootFrame.GoBack(new SuppressNavigationTransitionInfo());
            }
        }

        // Tags for IllustrationInfoAndCommentsNavigationView

        private NavigationViewTag? _illustrationInfo;

        private NavigationViewTag? _comments;
    }
}