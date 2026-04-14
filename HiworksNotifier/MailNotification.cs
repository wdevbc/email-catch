using System;
using System.Windows.Input;

namespace HiworksNotifier
{
    public class MailNotification
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Sender { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string BodyPreview { get; }
        public string RawSubject { get; } // For accurate deduplication
        public DateTime Date { get; set; }
        
        // Command to handle removal of this notification
        public ICommand RemoveCommand { get; set; }

        public MailNotification(string sender, string subject, DateTime date, Action<MailNotification>? removeAction)
        {
            Sender = sender;
            Subject = subject;
            RawSubject = subject;
            Date = date;
            BodyPreview = "";
            
            if (removeAction != null)
                RemoveCommand = new RelayCommand(_ => removeAction(this));
            else
                RemoveCommand = new RelayCommand(_ => { }); // No-op command
        }
    }

    // Simple RelayCommand implementation
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object? parameter) => _execute(parameter);
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
