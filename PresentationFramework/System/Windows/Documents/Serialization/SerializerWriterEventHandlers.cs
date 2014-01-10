/*++ 

    Copyright (C) 1985 - 2005 Microsoft Corporation
    All rights reserved.
 
    Module Name:
 
        XPSEventHandlers.hpp 

    Abstract: 

        EventHandlers used with the XpsDocumentWriter and XPSEmitter classes.

    Author: 

        Ali Naqvi (alinaqvi) - 25th May 2005 
 
    Revision History:
--*/ 
using System.Printing;
using System.Security;

namespace System.Windows.Documents.Serialization 
{
    /// <summary> 
    /// 
    /// </summary>
    public enum  WritingProgressChangeLevel 
    {
        /// <summary>
        ///
        /// </summary> 
        None                                 = 0,
        /// <summary> 
        /// 
        /// </summary>
        FixedDocumentSequenceWritingProgress = 1, 
        /// <summary>
        ///
        /// </summary>
        FixedDocumentWritingProgress         = 2, 
        /// <summary>
        /// 
        /// </summary> 
        FixedPageWritingProgress             = 3
    }; 

    //
    // The following are the event args giving the caller more information
    // about the previously describes events 
    //
 
    /// <summary> 
    ///
    /// </summary> 
    public class WritingPrintTicketRequiredEventArgs : EventArgs
    {

        /// <summary> 
        ///
        /// </summary> 
        /// <SecurityNote> 
        /// Critical    -  Argument PrintTicketLevel is considered critical because it is defined in non APTCA ReachFramework.dll
        /// TreatAsSafe -  PrintTicketLevel enum is safe 
        /// </SecurityNote>
        [SecurityCritical, SecurityTreatAsSafe]
        public WritingPrintTicketRequiredEventArgs(
            System.Windows.Xps.Serialization.PrintTicketLevel       printTicketLevel, 
            int                                                     sequence
            ) 
        { 
            _printTicketLevel = printTicketLevel;
            _sequence = sequence; 
        }


        /// <summary> 
        ///
        /// </summary> 
       public 
        System.Windows.Xps.Serialization.PrintTicketLevel
        CurrentPrintTicketLevel 
        {
            /// <SecurityNote>
            /// Critical    -   Return type PrintTicketLevel is critical because it is defined in non APTCA ReachFramework.dll
            /// TreatAsSafe -   PrintTicketLevel enum is safe 
            /// </SecurityNote>
            [SecurityCritical, SecurityTreatAsSafe] 
            get 
            {
                return _printTicketLevel; 
            }

        }
 
        /// <summary>
        /// 
        /// </summary> 
        public
        int 
        Sequence
        {
            get
            { 
                return _sequence;
            } 
 
        }
 
        /// <summary>
        ///
        /// </summary>
        public 
        PrintTicket
        CurrentPrintTicket 
        { 
            /// <SecurityNote>
            /// Critical    -   PrintTicket argument is critical because it is defined in non APTCA ReachFramework.dll 
            /// TreatAsSafe -   PrintTicket type is safe
            /// </SecurityNote>
            [SecurityCritical, SecurityTreatAsSafe]
            set 
            {
                _printTicket = value; 
            } 

            /// <SecurityNote> 
            /// Critical    -   PrintTicket return type is critical because it is defined in non APTCA ReachFramework.dll
            /// TreatAsSafe -   PrintTicketLevel enum is safe
            /// </SecurityNote>
            [SecurityCritical, SecurityTreatAsSafe] 
            get
            { 
                return _printTicket; 
            }
        } 



        /// <SecurityNote> 
        /// Critical    -   PrintTicketLevel type is critical because it is defined in non APTCA ReachFramework.dll
        /// TreatAsSafe -   PrintTicketLevel enum is safe 
        /// </SecurityNote> 
        [SecurityCritical, SecurityTreatAsSafe]
        private System.Windows.Xps.Serialization.PrintTicketLevel _printTicketLevel; 
        private int                                                         _sequence;

        /// <SecurityNote>
        /// Critical    -   Type is critical because it is defined in non APTCA ReachFramework.dll 
        /// TreatAsSafe -   PrintTicket type is safe
        /// </SecurityNote> 
        [SecurityCritical, SecurityTreatAsSafe] 
        private PrintTicket _printTicket;
 
    };

    /// <summary>
    /// 
    /// </summary>
    public  class WritingCompletedEventArgs : ComponentModel.AsyncCompletedEventArgs 
    { 
        /// <summary>
        /// 
        /// </summary>
        public
        WritingCompletedEventArgs(
            bool        cancelled, 
            Object      state,
            Exception   exception): base(exception, cancelled, state) 
        { 
        }
    }; 

    /// <summary>
    ///
    /// </summary> 
    public class WritingProgressChangedEventArgs : ComponentModel.ProgressChangedEventArgs
    { 
        /// <summary> 
        ///
        /// </summary> 
        public
        WritingProgressChangedEventArgs(
            WritingProgressChangeLevel   	writingLevel,
            int                             number, 
            int                             progressPercentage,
            Object                          state): base(progressPercentage, state) 
        { 
            _number       = number;
            _writingLevel = writingLevel; 
        }

        /// <summary>
        /// 
        /// </summary>
        public 
        int 
        Number
        { 
            get
            {
                return _number;
            } 
        }
 
        /// <summary> 
        ///
        /// </summary> 
        public
        WritingProgressChangeLevel
        WritingLevel
        { 
            get
            { 
                return _writingLevel; 
            }
 
        }

        private int                             _number;
 
        private WritingProgressChangeLevel      _writingLevel;
    }; 
 
    //
    // The following are the event args giving the caller more information 
    // about a cancel occuring event
    //
     /// <summary>
    /// 
    /// </summary>
   public  class WritingCancelledEventArgs : EventArgs 
    { 
        /// <summary>
        /// 
        /// </summary>
        public
        WritingCancelledEventArgs(
            Exception       exception 
            )
        { 
            _exception = exception; 
        }
 
        /// <summary>
        ///
        /// </summary>
        public 
        Exception
        Error 
        { 
            get
            { 
                return _exception;
            }
        }
 

        private Exception      _exception; 
 
    };
 


    //
    // The following are the delegates used to represent the following 3 events 
    // - Getting the PrintTicket from the calling code
    // - Informing the calling code that the write operation has completed 
    // - Informing the calling code of the progress in the write operation 
    // - Informing the caller code that the oepration was cancelled
    // 
    /// <summary>
    ///
    /// </summary>
    public 
    delegate
    void 
    WritingPrintTicketRequiredEventHandler( 
         Object                                 sender,
         WritingPrintTicketRequiredEventArgs    e 
         );

    /// <summary>
    /// 
    /// </summary>
    public 
    delegate 
    void
    WritingProgressChangedEventHandler( 
        Object                              sender,
        WritingProgressChangedEventArgs     e
        );
 
    /// <summary>
    /// 
    /// </summary> 
    public
    delegate 
    void
    WritingCompletedEventHandler(
        Object                     sender,
        WritingCompletedEventArgs   e 
        );
 	 
    /// <summary> 
    ///
    /// </summary> 
    public
    delegate
    void
    WritingCancelledEventHandler( 
        Object                     sender,
        WritingCancelledEventArgs   e 
        ); 
}

