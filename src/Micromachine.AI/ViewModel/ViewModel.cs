﻿using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Micromachine.AI.Service;

namespace Micromachine.AI.ViewModel
{
    internal class ViewModel : BaseViewModel
    {
        private readonly ImageService _imageService;

        private ICommand _autoModeCommand;

        private ICommand _resetNetworkCommand;

        private ICommand _someCommand;

        public object Locker = new object(); // used to synchronise camera data between threads

        public ViewModel()
        {
            WriteableBitmap writeableBitmap;
            var cameraGrid = new RectangularCameraGrid(10, 10);
            this._imageService = new ImageService(this, cameraGrid);
            this.ImageSource = writeableBitmap = this._imageService.CreateImage(900, 600);

            this.Car = new Car(new NNBrain(cameraGrid.TotalPoints)); // Create a car and neural network 'brain'

            CompositionTarget.Rendering += (o, e) =>
            {
                UpdateCarCoordinates();
                this._imageService.UpdateImage(writeableBitmap);
            };
        }

        public Car Car { get; }

        public ObservableCollection<string> Logs { get; set; } = new ObservableCollection<string>();

        public ICommand TeachCommand
        {
            get { return this._someCommand ?? (this._someCommand = new RelayCommand(o => true, o => Teach((Direction)int.Parse((string)o)))); }
        }

        public ICommand AutoModeCommand
        {
            get
            {
                return this._autoModeCommand ?? (this._autoModeCommand = new RelayCommand(o => true, o =>
                {
                    this.Car.AutoPilot = !this.Car.AutoPilot;
                    Log($"AutoMode = {this.Car.AutoPilot}");
                }));
            }
        }

        public ICommand ResetNetworkCommand
        {
            get
            {
                return this._resetNetworkCommand ?? (this._resetNetworkCommand = new RelayCommand(o => true, o =>
                {
                    this.Car.Brain.Reset();
                    Log("Reset");
                }));
            }
        }

        private void Log(string s)
        {
            this.Logs.Add(s);
            if (this.Logs.Count > 5)
            {
                this.Logs.RemoveAt(0);
            }
        }

        public void Teach(Direction direction)
        {
            Log($"Teach {direction}");

            lock (this.Locker)
            {
                this.Car.AddTrainingData((float[])this._imageService.CameraInput.Clone(), direction);
            }

            this.Car.Train();
        }

        public void UpdateCarCoordinates()
        {
            if (this.Car.AutoPilot)
            {
                lock (this.Locker)
                {
                    this.Car.Evaluate(this._imageService.CameraInput);
                }
            }
            else
            {
                if (Keyboard.IsKeyDown(Key.Right) && Keyboard.Modifiers == ModifierKeys.None)
                {
                    this.Car.Rotate(Direction.Right);
                }

                if (Keyboard.IsKeyDown(Key.Left) && Keyboard.Modifiers == ModifierKeys.None)
                {
                    this.Car.Rotate(Direction.Left);
                }

                if (Keyboard.IsKeyDown(Key.Up) && Keyboard.Modifiers == ModifierKeys.None)
                {
                    this.Car.Accelerate();
                }
                else if (Keyboard.IsKeyDown(Key.Down) && Keyboard.Modifiers == ModifierKeys.None)
                {
                    this.Car.Reverse();
                }
                else
                {
                    this.Car.Deccelerate();
                }
            }

            this.Car.UpdateCarCoordinates();
        }

        #region ImageSource

        private ImageSource _imageSource;

        public ImageSource ImageSource
        {
            get => this._imageSource;
            set
            {
                if (!Equals(this._imageSource, value))
                {
                    this._imageSource = value;
                    OnPropertyChanged(nameof(this.ImageSource));
                }
            }
        }

        #endregion
    }
}