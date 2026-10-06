using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Parking.Application.Dto;
using Parking.UI.Windows.ViewModels;

namespace Parking.UI.Windows.View.Pages
{
    public partial class DashboardPage : UserControl
    {
        // Estado del gesto de arrastre de una tarjeta de puesto.
        private Point _dragStartPoint;
        private bool _isPotentialDrag;
        private bool _dragInProgress;
        private Button? _dragSourceButton;
        private ParkingSlotDashboardItemDTO? _draggedItem;

        public DashboardPage()
        {
            InitializeComponent();

            // Escucha también los eventos que el botón de la tarjeta ya marcó como atendidos.
            this.AddHandler(PreviewMouseLeftButtonDownEvent,
                new MouseButtonEventHandler(OnPreviewMouseLeftButtonDown), true);
            this.AddHandler(PreviewMouseMoveEvent,
                new MouseEventHandler(OnPreviewMouseMove), true);
            this.AddHandler(PreviewMouseLeftButtonUpEvent,
                new MouseButtonEventHandler(OnPreviewMouseLeftButtonUp), true);
        }

        /// <summary>Recuerda la tarjeta sobre la que comenzó el gesto.</summary>
        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragSourceButton = FindVisualParent<Button>(e.OriginalSource as DependencyObject);
            _draggedItem = _dragSourceButton?.DataContext as ParkingSlotDashboardItemDTO;

            if (_draggedItem != null)
            {
                _dragStartPoint = e.GetPosition(this);
                _isPotentialDrag = true;
            }
            else
            {
                _isPotentialDrag = false;
            }
        }

        /// <summary>Inicia el arrastre al superar el umbral de movimiento de Windows.</summary>
        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isPotentialDrag || e.LeftButton != MouseButtonState.Pressed)
                return;

            var currentPosition = e.GetPosition(this);

            if (Math.Abs(currentPosition.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(currentPosition.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                _isPotentialDrag = false;
                _dragInProgress = true;

                DependencyObject dragSource = _dragSourceButton ?? (DependencyObject)this;
                try
                {
                    DragDrop.DoDragDrop(dragSource, _draggedItem!, DragDropEffects.Move);
                }
                finally
                {
                    _dragInProgress = false;
                }
            }
        }

        /// <summary>Evita que soltar una tarjeta active también su clic.</summary>
        private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_dragInProgress)
            {
                e.Handled = true;
                _dragInProgress = false;
            }

            _isPotentialDrag = false;
        }

        /// <summary>Atenúa la tarjeta que recibirá el puesto arrastrado.</summary>
        private void Slot_DragEnter(object sender, DragEventArgs e)
        {
            SetSlotOpacity(sender, 0.6);
        }

        /// <summary>Restaura el aspecto de la tarjeta al retirar el arrastre.</summary>
        private void Slot_DragLeave(object sender, DragEventArgs e)
        {
            SetSlotOpacity(sender, 1.0);
        }

        private async void Slot_Drop(object sender, DragEventArgs e)
        {
            var targetButton = sender as Button;
            if (targetButton == null) return;

            var draggedItem = e.Data.GetData(typeof(ParkingSlotDashboardItemDTO)) as ParkingSlotDashboardItemDTO;
            var targetItem = targetButton.DataContext as ParkingSlotDashboardItemDTO;

            if (draggedItem != null && targetItem != null && draggedItem != targetItem)
            {
                if (DataContext is not DashboardViewModel viewModel) return;

                // Intercambiamos posiciones (con notificación en tiempo real)
                var tempX = draggedItem.PositionX;
                var tempY = draggedItem.PositionY;

                draggedItem.PositionX = targetItem.PositionX;
                draggedItem.PositionY = targetItem.PositionY;
                targetItem.PositionX = tempX;
                targetItem.PositionY = tempY;

                await viewModel.SaveSlotPositionsAsync();
            }

            SetSlotOpacity(targetButton, 1.0);
        }

        /// <summary>Permite soltar un puesto en el espacio libre del tablero.</summary>
        private void Canvas_DragEnter(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Move;
        }

        private void Canvas_DragLeave(object sender, DragEventArgs e)
        {
        }

        private async void Canvas_Drop(object sender, DragEventArgs e)
        {
            var canvas = sender as Canvas;
            if (canvas == null) return;

            var draggedItem = e.Data.GetData(typeof(ParkingSlotDashboardItemDTO)) as ParkingSlotDashboardItemDTO;
            if (draggedItem == null) return;

            var position = e.GetPosition(canvas);

            draggedItem.PositionX = (int)position.X - 80;
            draggedItem.PositionY = (int)position.Y - 50;

            if (DataContext is DashboardViewModel viewModel)
                await viewModel.SaveSlotPositionsAsync();
        }

        /// <summary>Aplica la opacidad a la tarjeta de destino si contiene un borde.</summary>
        private static void SetSlotOpacity(object source, double opacity)
        {
            if (source is Button button && FindVisualChild<Border>(button) is { } border)
                border.Opacity = opacity;
        }

        /// <summary>Busca el primer ancestro del tipo indicado.</summary>
        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T match) return match;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        /// <summary>Busca el primer descendiente visual del tipo indicado.</summary>
        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t) return t;
                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null) return childOfChild;
            }
            return null;
        }
    }
}
