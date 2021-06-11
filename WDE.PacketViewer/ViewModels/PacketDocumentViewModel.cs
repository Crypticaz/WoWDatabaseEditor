using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using DynamicData;
using DynamicData.Binding;
using WDE.Common;
using WDE.Common.Database;
using WDE.Common.History;
using WDE.Common.Managers;
using WDE.Common.Services;
using WDE.Common.Services.MessageBox;
using WDE.Common.Tasks;
using WDE.Common.Types;
using WDE.Common.Utils;
using WDE.Module.Attributes;
using WDE.MVVM;
using WDE.MVVM.Observable;
using WDE.PacketViewer.Filtering;
using WDE.PacketViewer.PacketParserIntegration;
using WDE.PacketViewer.Processing.Processors;
using WDE.PacketViewer.Solutions;
using WoWPacketParser.Proto;

namespace WDE.PacketViewer.ViewModels
{
    [AutoRegister]
    public class PacketDocumentViewModel : ObservableBase, ISolutionItemDocument, IProgress<float>
    {
        private readonly IMainThread mainThread;
        private readonly MostRecentlySearchedService mostRecentlySearchedService;
        private readonly IDatabaseProvider databaseProvider;
        private readonly IGenericTableDocumentService genericTableDocumentService;
        private readonly IMessageBoxService messageBoxService;
        private readonly IPacketFilteringService filteringService;
        private readonly ISniffLoader sniffLoader;

        private readonly PacketDocumentSolutionItem solutionItem;

        public PacketDocumentViewModel(PacketDocumentSolutionItem solutionItem, 
            IMainThread mainThread,
            MostRecentlySearchedService mostRecentlySearchedService,
            IDatabaseProvider databaseProvider,
            IGenericTableDocumentService genericTableDocumentService,
            Func<INativeTextDocument> nativeTextDocumentCreator,
            IMessageBoxService messageBoxService,
            IPacketFilteringService filteringService,
            ISniffLoader sniffLoader)
        {
            this.solutionItem = solutionItem;
            this.mainThread = mainThread;
            this.mostRecentlySearchedService = mostRecentlySearchedService;
            this.databaseProvider = databaseProvider;
            this.genericTableDocumentService = genericTableDocumentService;
            this.messageBoxService = messageBoxService;
            this.filteringService = filteringService;
            this.sniffLoader = sniffLoader;
            MostRecentlySearched = mostRecentlySearchedService.MostRecentlySearched;
            Title = "Sniff " + Path.GetFileNameWithoutExtension(solutionItem.File);
            SolutionItem = solutionItem;
            FilterText = nativeTextDocumentCreator();
            SelectedPacketPreview = nativeTextDocumentCreator();

            Watch(this, t => t.FilteringProgress, nameof(ProgressUnknown));
            
            filteredPackets = AllPackets;
            ApplyFilterCommand = new AsyncCommand(async () =>
            {
                if (inApplyFilterCommand)
                    return;

                inApplyFilterCommand = true;
                MostRecentlySearchedItem = FilterText.ToString();
                inApplyFilterCommand = false;
                
                if (!string.IsNullOrEmpty(FilterText.ToString()))
                    mostRecentlySearchedService.Add(FilterText.ToString());
                
                filteringToken?.Cancel();
                filteringToken = null;
                await FilterPackets(FilterText.ToString());
            });

            LoadSniff();

            AutoDispose(this.ToObservable(t => t.SelectedPacket).SubscribeAction(doc =>
            {
                if (doc != null)
                    SelectedPacketPreview.FromString(doc.Text);
            }));
        }

        public void Report(float value)
        {
            FilteringProgress = value * 100f;   
        }
        
        private async Task LoadSniff()
        {
            LoadingInProgress = true;
            FilteringInProgress = true;

            try
            {
                var packets = await sniffLoader.LoadSniff(solutionItem.File, this);

                var entryExtractor = new EntryExtractorProcessor();
                using (AllPackets.SuspendNotifications())
                {
                    foreach (var packet in packets.Packets_)
                    {
                        var entry = entryExtractor.Process(packet);
                        AllPackets.Add(new PacketViewModel(packet, packet.BaseData.Time.ToDateTime(), entry));
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            LoadingInProgress = false;
            ApplyFilterCommand.ExecuteAsync();
        }

        private ObservableCollection<PacketViewModel> filteredPackets;
        public ObservableCollection<PacketViewModel> FilteredPackets
        {
            get => filteredPackets;
            set => SetProperty(ref filteredPackets, value);
        }

        public ObservableCollectionExtended<PacketViewModel> AllPackets { get; } = new ObservableCollectionExtended<PacketViewModel>();
        public ReadOnlyObservableCollection<string> MostRecentlySearched { get; }
        
        private string? mostRecentlySearchedItem;
        public string? MostRecentlySearchedItem
        {
            get => mostRecentlySearchedItem;
            set
            {
                if (mostRecentlySearchedItem == value)
                    return;

                if (value != null && !MostRecentlySearched.Contains(value))
                    value = null;
                
                SetProperty(ref mostRecentlySearchedItem, value);
                if (value != null)
                {
                    FilterText.FromString(value);
                    ApplyFilterCommand.ExecuteAsync();   
                }
            }
        }

        private PacketViewModel? selectedPacket;
        public PacketViewModel? SelectedPacket
        {
            get => selectedPacket;
            set => SetProperty(ref selectedPacket, value);
        }

        private float filteringProgress;
        public float FilteringProgress
        {
            get => filteringProgress;
            set => SetProperty(ref filteringProgress, value);
        }

        public bool ProgressUnknown => filteringProgress < 0;
        
        private bool filteringInProgress;
        public bool FilteringInProgress
        {
            get => filteringInProgress;
            set => SetProperty(ref filteringInProgress, value);
        }
        
        private bool loadingInProgress;
        public bool LoadingInProgress
        {
            get => loadingInProgress;
            set => SetProperty(ref loadingInProgress, value);
        }
        
        private bool inApplyFilterCommand = false;
        public AsyncCommand ApplyFilterCommand { get; }

        public INativeTextDocument SelectedPacketPreview { get; }
        public INativeTextDocument FilterText { get; }
        
        private CancellationTokenSource? filteringToken = null;
        private async Task FilterPackets(string filter)
        {
            FilteringInProgress = true;
            var tokenSource = new CancellationTokenSource();
            filteringToken = tokenSource;

            try {
                var result = await filteringService.Filter(AllPackets, filter, tokenSource.Token, this);
                if (result != null)
                    FilteredPackets = result;
            }
            catch (Exception e)
            {
                messageBoxService.ShowDialog<bool>(new MessageBoxFactory<bool>()
                    .SetTitle("Filtering error")
                    .SetMainInstruction("Filtering error")
                    .SetContent(e.Message)
                    .WithOkButton(true)
                    .Build());
            }

            if (filteringToken == tokenSource)
            {
                filteringToken = null;
                FilteringInProgress = false;
            }
        }
        
        public string Title { get; }
        public ImageUri? Icon => new ImageUri("Icons/document_sniff.png");
        public ICommand Undo => AlwaysDisabledCommand.Command;
        public ICommand Redo => AlwaysDisabledCommand.Command;
        public ICommand Copy => AlwaysDisabledCommand.Command;
        public ICommand Cut => AlwaysDisabledCommand.Command;
        public ICommand Paste => AlwaysDisabledCommand.Command;
        public ICommand Save => AlwaysDisabledCommand.Command;
        public IAsyncCommand? CloseCommand { get; set; }
        public bool CanClose => true;
        public bool IsModified => false;
        public IHistoryManager? History => null;
        public ISolutionItem SolutionItem { get; }
    }
}