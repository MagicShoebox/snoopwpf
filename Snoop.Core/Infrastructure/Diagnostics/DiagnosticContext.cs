﻿namespace Snoop.Infrastructure.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using JetBrains.Annotations;
    using Snoop.Data.Tree;
    using Snoop.Infrastructure.Diagnostics.Providers;

    public sealed class DiagnosticContext : IDisposable, INotifyPropertyChanged
    {
        private readonly ObservableCollection<DiagnosticProvider> diagnosticProviders = new();
        private readonly ObservableCollection<DiagnosticItem> diagnosticItems = new();

        public DiagnosticContext(TreeService treeService)
            : this()
        {
            this.TreeService = treeService;
        }

        public DiagnosticContext()
        {
            this.DiagnosticProviders = new(this.diagnosticProviders);
            this.DiagnosticItems = new(this.diagnosticItems);

            this.diagnosticProviders.Add(new FreezeFreezablesDiagnosticProvider());
            this.diagnosticProviders.Add(new LocalResourceDefinitionsDiagnosticProvider());
            this.diagnosticProviders.Add(new NonVirtualizedListsDiagnosticProvider());
            this.diagnosticProviders.Add(new UnresolvedDynamicResourceDiagnosticProvider());

            foreach (var diagnosticProvider in this.diagnosticProviders)
            {
                diagnosticProvider.PropertyChanged += this.HandleDiagnosticProviderOnPropertyChanged;
            }

            // Ensure the helper is created
            _ = BindingDiagnosticHelper.Instance;
        }

        public TreeService? TreeService { get; }

        public ReadOnlyObservableCollection<DiagnosticProvider> DiagnosticProviders { get; }

        public ReadOnlyObservableCollection<DiagnosticItem> DiagnosticItems { get; }

        public void Add(DiagnosticItem item)
        {
            this.diagnosticItems.Add(item);
        }

        public void AddRange(IEnumerable<DiagnosticItem> items)
        {
            foreach (var item in items)
            {
                this.diagnosticItems.Add(item);
            }
        }

        public void TreeItemDisposed(TreeItem treeItem)
        {
            foreach (var item in this.diagnosticItems.ToList())
            {
                if (item.TreeItem == treeItem)
                {
                    this.diagnosticItems.Remove(item);
                }
            }
        }

        public void Analyze(TreeItem item)
        {
            foreach (var diagnosticProvider in this.DiagnosticProviders)
            {
                this.Analyze(item, diagnosticProvider);
            }
        }

        public void Analyze(TreeItem item, DiagnosticProvider diagnosticProvider)
        {
            this.AddRange(diagnosticProvider.GetDiagnosticItems(item));
        }

        public void AnalyzeTree()
        {
            if (this.TreeService?.RootTreeItem is null)
            {
                return;
            }

            this.AnalyzeTree(this.TreeService.RootTreeItem);
        }

        public void AnalyzeTree(TreeItem item)
        {
            this.Analyze(item);

            if (item.OmitChildren == false)
            {
                foreach (var child in item.Children)
                {
                    this.Analyze(child);
                }
            }
        }

        public void AnalyzeTree(TreeItem item, DiagnosticProvider diagnosticProvider)
        {
            this.Analyze(item, diagnosticProvider);

            if (item.OmitChildren == false)
            {
                foreach (var child in item.Children)
                {
                    this.Analyze(child, diagnosticProvider);
                }
            }
        }

        private void HandleDiagnosticProviderOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName?.Equals(nameof(DiagnosticProvider.IsActive), StringComparison.Ordinal) == true
                && sender is DiagnosticProvider diagnosticProvider)
            {
                // Always remove the diagnostics from the affected provider first
                foreach (var diagnosticItem in this.diagnosticItems.Where(x => ReferenceEquals(x.DiagnosticProvider, diagnosticProvider)).ToList())
                {
                    this.diagnosticItems.Remove(diagnosticItem);
                }

                if (diagnosticProvider.IsActive
                    && this.TreeService?.RootTreeItem is not null)
                {
                    this.AnalyzeTree(this.TreeService.RootTreeItem, diagnosticProvider);
                }
            }
        }

        public void Dispose()
        {
            foreach (var provider in this.DiagnosticProviders)
            {
                provider.Dispose();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}