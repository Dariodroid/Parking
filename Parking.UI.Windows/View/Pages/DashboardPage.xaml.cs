using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Parking.Application.Dto;

namespace Parking.UI.Windows.View.Pages
{
    public partial class DashboardPage : UserControl
    {
        // 🟢 Estado de la "directora con walkie-talkie"
        private Point _dragStartPoint;
        private bool _isPotentialDrag;
        private bool _dragInProgress;
        private Button _dragSourceButton;
        private ParkingSlotDashboardItemDTO _draggedItem;

        public DashboardPage()
        {
            InitializeComponent();

            // 🟢 REGISTRO BLINDADO: handledEventsToo = true
            // La raíz escucha TODOS los eventos de mouse, incluso si el Button
            // los marca como "manejados" o captura el mouse.
            this.AddHandler(PreviewMouseLeftButtonDownEvent,
                new MouseButtonEventHandler(OnPreviewMouseLeftButtonDown), true);
            this.AddHandler(PreviewMouseMoveEvent,
                new MouseEventHandler(OnPreviewMouseMove), true);
            this.AddHandler(PreviewMouseLeftButtonUpEvent,
                new MouseButtonEventHandler(OnPreviewMouseLeftButtonUp), true);
        }

        // ==========================================
        // PASO 1: PRESIONAR (¿Es una tarjeta o es otro botón?)
        // ==========================================
        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragSourceButton = FindVisualParent<Button>(e.OriginalSource as DependencyObject);
            _draggedItem = _dragSourceButton?.DataContext as ParkingSlotDashboardItemDTO;

            // Solo activamos el arrastre potencial si es una tarjeta de puesto.
            // (El botón "RESTABLECER ORDEN" tiene otro DataContext y queda excluido).
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

        // ==========================================
        // PASO 2: MOVER (¿Palmadita o agarre?)
        // ==========================================
        // ==========================================
        // PASO 2: MOVER (¿Palmadita o agarre?)
        // ==========================================
        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isPotentialDrag || e.LeftButton != MouseButtonState.Pressed)
                return;

            var currentPosition = e.GetPosition(this);

            // 🟢 Umbral del sistema: si se movió lo suficiente, es ARRASTRE
            if (Math.Abs(currentPosition.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(currentPosition.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                _isPotentialDrag = false;
                _dragInProgress = true;

                DependencyObject dragSource = _dragSourceButton ?? (DependencyObject)this;

                // 🟢 CORREGIDO: try/finally garantiza que _dragInProgress se resetee
                // incluso si DoDragDrop consume el MouseUp o lanza una excepción.
                try
                {
                    DragDrop.DoDragDrop(dragSource, _draggedItem, DragDropEffects.Move);
                }
                finally
                {
                    _dragInProgress = false;
                }
            }
        }

        // ==========================================
        // PASO 3: SOLTAR (Suprimir el clic si hubo arrastre)
        // ==========================================
        private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_dragInProgress)
            {
                // Si acabamos de arrastrar, marcamos el evento como manejado
                // para que el Button NO dispare su Click (evita efectos secundarios).
                e.Handled = true;
                _dragInProgress = false;
            }

            // Si no hubo arrastre, fue un CLIC limpio: el Button ejecutará
            // su SelectSlotCommand y el panel de detalle se cargará.
            _isPotentialDrag = false;
        }

        // ==========================================
        // EVENTOS DE LAS TARJETAS (DROP SOBRE OTRA TARJETA)
        // ==========================================

        private void Slot_DragEnter(object sender, DragEventArgs e)
        {
            var targetButton = sender as Button;
            if (targetButton != null)
            {
                var border = FindVisualChild<Border>(targetButton);
                if (border != null)
                {
                    border.Opacity = 0.6;
                }
            }
        }

        private void Slot_DragLeave(object sender, DragEventArgs e)
        {
            var targetButton = sender as Button;
            if (targetButton != null)
            {
                var border = FindVisualChild<Border>(targetButton);
                if (border != null)
                {
                    border.Opacity = 1.0;
                }
            }
        }

        private async void Slot_Drop(object sender, DragEventArgs e)
        {
            var targetButton = sender as Button;
            if (targetButton == null) return;

            var draggedItem = e.Data.GetData(typeof(ParkingSlotDashboardItemDTO)) as ParkingSlotDashboardItemDTO;
            var targetItem = targetButton.DataContext as ParkingSlotDashboardItemDTO;

            if (draggedItem != null && targetItem != null && draggedItem != targetItem)
            {
                var viewModel = this.DataContext;
                if (viewModel == null) return;

                // Intercambiamos posiciones (con notificación en tiempo real)
                var tempX = draggedItem.PositionX;
                var tempY = draggedItem.PositionY;

                draggedItem.PositionX = targetItem.PositionX;
                draggedItem.PositionY = targetItem.PositionY;
                targetItem.PositionX = tempX;
                targetItem.PositionY = tempY;

                var saveMethod = viewModel.GetType().GetMethod("SaveSlotPositionsAsync", BindingFlags.Public | BindingFlags.Instance);
                if (saveMethod != null)
                {
                    var saveTask = (Task)saveMethod.Invoke(viewModel, null);
                    if (saveTask != null)
                    {
                        await saveTask;
                    }
                }
            }

            var border = FindVisualChild<Border>(targetButton);
            if (border != null) border.Opacity = 1.0;
        }

        // ==========================================
        // EVENTOS DEL CANVAS (DROP EN ESPACIO VACÍO)
        // ==========================================

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

            var viewModel = this.DataContext;
            if (viewModel != null)
            {
                var saveMethod = viewModel.GetType().GetMethod("SaveSlotPositionsAsync", BindingFlags.Public | BindingFlags.Instance);
                if (saveMethod != null)
                {
                    var saveTask = (Task)saveMethod.Invoke(viewModel, null);
                    if (saveTask != null)
                    {
                        await saveTask;
                    }
                }
            }
        }

        // ==========================================
        // MÉTODOS AUXILIARES (HELPERS DEL ÁRBOL VISUAL)
        // ==========================================

        public static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            T parent = parentObject as T;
            if (parent != null) return parent;
            else return FindVisualParent<T>(parentObject);
        }

        public static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
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