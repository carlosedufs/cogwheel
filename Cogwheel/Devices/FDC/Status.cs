using System;

namespace BeeDevelopment.Cogwheel.Devices {
    public partial class FDC {

        [Flags]
        public enum MainStatus {
            None              = 0x00,
            Fdd0Busy          = 0x01, // D0B
            Fdd1Busy          = 0x02, // D1B
            Fdd2Busy          = 0x03, // D2B
            Fdd3Busy          = 0x04, // D3B
            FdcBusy           = 0x10, // CB
            ExecutionModeBusy = 0x20, // EXM
            DataIO            = 0x40, // DIO
            RequestForMaster  = 0x80, // ROM
        }

        [Flags]
        public enum Status0 {
            None = 0x00,
            UnitSelect0 = 0x01,
            UnitSelect1 = 0x02,
            HeadAddress = 0x04,
            NotReady = 0x08,
            EquipmentCheck = 0x10,
            SeekEnd = 0x20,
        }

        [Flags]
        public enum Status1 {
            None = 0x00,
            MissingAddressMark = 0x01,
            NotWritable = 0x02,
            NoData = 0x04,
            Overrun = 0x10,
            DataError = 0x20,
            EndOfCylinder = 0x80,
        }

        [Flags]
        public enum Status2 {
            None = 0x00,
            MissingAddressMarkInDataField = 0x01,
            BadCylinder = 0x02,
            ScanNotSatisfied = 0x04,
            ScanEqualHit = 0x08,
            WrongCylinder = 0x10,
            DataErrorInDataField = 0x20,
            ControlMark = 0x40,
        }

        [Flags]
        public enum Status3 {
            None = 0x00,
            UnitSelect0 = 0x01,
            UnitSelect1 = 0x02,
            HeadAddress = 0x04,
            TwoSide = 0x08,
            Track0 = 0x10,
            Ready = 0x20,
            WriteProtected = 0x40,
            Fault = 0x80,
        }

        private MainStatus CurrentMainStatus = MainStatus.None;
        public Status0 CurrentStatus0 = Status0.None;
        public Status1 CurrentStatus1 = Status1.None;
        public Status2 CurrentStatus2 = Status2.None;
        public Status3 CurrentStatus3 = Status3.None;

        public byte ReadStatus() {
            return (byte)CurrentMainStatus;
        }

    }
}
