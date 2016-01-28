#include "SerialPort.h"
#include <errno.h>
#include <termios.h>
#include <unistd.h>
#include <string.h>
#include <stdio.h>
#include <sys/stat.h>
#include <fcntl.h>
#include <stdint.h>
#include <stdlib.h>

#include <string>
#include <iostream>
#include <thread>
#include <chrono>

using namespace std;

static int
set_interface_attribs (int fd, int speed, int parity)
{
   struct termios tty;
   memset (&tty, 0, sizeof tty);
   if (tcgetattr (fd, &tty) != 0)
   {
      //error_message ("error %d from tcgetattr", errno);
      return -1;
   }

   cfsetospeed (&tty, speed);
   cfsetispeed (&tty, speed);

   tty.c_cflag = (tty.c_cflag & ~CSIZE) | CS8;     // 8-bit chars
   // disable IGNBRK for mismatched speed tests; otherwise receive break
   // as \000 chars
   tty.c_iflag &= ~IGNBRK;         // disable break processing
   tty.c_lflag = 0;                // no signaling chars, no echo,
                                        // no canonical processing
   tty.c_oflag = 0;                // no remapping, no delays
   tty.c_cc[VMIN]  = 0;            // read doesn't block
   tty.c_cc[VTIME] = 0;            // was 1 = 0.1 seconds read timeout

   tty.c_iflag &= ~(IXON | IXOFF | IXANY); // shut off xon/xoff ctrl

   tty.c_cflag |= (CLOCAL | CREAD);// ignore modem controls,
                                   // enable reading
   tty.c_cflag &= ~(PARENB | PARODD);      // shut off parity
   tty.c_cflag |= parity;
   tty.c_cflag &= ~CSTOPB;
   tty.c_cflag &= ~CRTSCTS;

   if (tcsetattr (fd, TCSANOW, &tty) != 0)
   {
      //error_message ("error %d from tcsetattr", errno);
      return -1;
   }
   return 0;
}

static int
set_blocking (int fd, int should_block)
{
   struct termios tty;
   memset (&tty, 0, sizeof tty);
   if (tcgetattr (fd, &tty) != 0)
   {
      //error_message ("error %d from tggetattr", errno);
      return -1;
   }

   tty.c_cc[VMIN]  = should_block ? 1 : 0;
   tty.c_cc[VTIME] = 0;            // was 1 = 0.1 seconds read timeout

   if (tcsetattr (fd, TCSANOW, &tty) != 0)
   {
      //error_message ("error %d setting term attributes", errno);
      return -1;
   }
   return 0;
}


static int
GetBaudRate(string strBaud)
{
// Only look for the most common baud rates
   if (strBaud.compare("19200") == 0)
   {
      return B19200;
   }
   else if (strBaud.compare("38400") == 0)
   {
      return B38400;
   }
   else if (strBaud.compare("57600") == 0)
   {
      return B57600;
   }
   else if (strBaud.compare("115200") == 0)
   {
      return B115200;
   }
   else if (strBaud.compare("230400") == 0)
   {
      return B230400;
   }
   else if (strBaud.compare("460800") == 0)
   {
      return B460800;
   }
   else if (strBaud.compare("500000") == 0)
   {
      return B500000;
   }
   else if (strBaud.compare("576000") == 0)
   {
      return B576000;
   }
   else if (strBaud.compare("921600") == 0)
   {
      return B921600;
   }
   return B115200;
}


static int
GetBaudRate(int nBaud)
{
// Only look for the most common baud rates
   switch( nBaud )
   {
      case 19200:
         return B19200;

      case 38400:
         return B38400;

      case 57600:
         return B57600;

      case 115200:
         return B115200;

      case 230400:
         return B230400;

      case 460800:
         return B460800;

      case 500000:
         return B500000;

      case 576000:
         return B576000;

      case 921600:
         return B921600;

      default:
         return B115200;
   }
   return B115200;
}


// Returns handle to serial port if successful
// otherwise, returns negative value
// nBaudRate - the baud rate to use
// nParity - parity to use (0 = no parity, 1 = odd, 2 = even)

int32_t SerialPort_Open( const char *pszPortName, 
                         uint32_t nBaudRate,
                         uint32_t nParity )
{
   int nBaudConstant = GetBaudRate( nBaudRate );
   int nParityMask = 0;
   switch( nParity )
   {
      case 1:
         nParityMask = PARENB | PARODD;
      break;

      case 2:
         nParityMask = PARENB;
      break;

      default:
         nParityMask = 0;
      break;
   }

//   printf( "SerialPort_Open: Try to open %s\n", pszPortName );

   int32_t fd = open( pszPortName, O_RDWR | O_NOCTTY | O_SYNC );
   if( fd < 0 )
   {
      //printf ("error %d opening %s: %s", errno, portname.c_str(), strerror (errno));
      return -1;
   }
   
   if( set_interface_attribs( fd, nBaudConstant, nParityMask ) != 0 )
   {
      close( fd );
      return -1;
   }

   if( set_blocking( fd, 0 ) != 0 )   // set no blocking
   {
      close( fd );
      return -1;
   }

//   printf( "SerialPort_Open: Success! Handle %d\n", fd );

   return fd;
}


void SerialPort_Close( int32_t fd )
{
   close( fd );
}


int32_t SerialPort_Write_ByteArray( int32_t fd, 
                                    const uint8_t* cmdBytes, 
                                    uint32_t siz )
{
   return (int32_t)write( fd, cmdBytes, siz );
}


int32_t SerialPort_Write_StringA( int32_t fd, 
                                  const char* pszCmd )
{
//   printf( "SerialPort_Write_StringA: Write %s\n", pszCmd );
   return (int32_t)write( fd, pszCmd, strlen( pszCmd ) );
}


int32_t SerialPort_Read_ByteArray( int32_t fd,
                                   uint8_t* respBytes,
                                   uint32_t nMaxBytes, 
                                   uint32_t nTimeoutMS )
{
   int nDelay = 10;
   int32_t nBytesToRead = nMaxBytes;
   int32_t nOffset = 0;
   int32_t nBytesRead = 0;
   int nConsecutiveEmptyReads = 0;

//   printf( "SerialPort_Read_ByteArray: Handle %d, MaxBytes %d, Timeout %d\n",
//      fd, nMaxBytes, nTimeoutMS );

   // Read until nMaxBytes have been read or we timed out
   for (int i = 0; (i < nTimeoutMS) && (nBytesToRead > 0); i += nDelay)
   {
      int nRead = read( fd, &respBytes[ nOffset ], nBytesToRead );
      if (nRead > 0)
      {
//         printf("read returned %d\n", nRead);
         nOffset += nRead;
         nBytesToRead -= nRead;
         nBytesRead += nRead;
         nConsecutiveEmptyReads = 0;
      }
      else
      {
         if( nBytesRead > 0 )
         {
            nConsecutiveEmptyReads++;
            if( nConsecutiveEmptyReads >= 2 )
            {
               break;
            }
         }
         //printf( "Read returned %d\n", nRead );
      }
      // else, wait for some bytes
      std::this_thread::sleep_for( std::chrono::milliseconds( nDelay ) );
   }
//   printf( "Returning %d\n", nBytesRead );
   return nBytesRead;
}


int main(int argc, char *argv[] )
{
   string portname = "/dev/ttyUSB0";
   int nBaudRate = 115200;
   int nBaudRateMask = B115200;

   printf("Command line args\n");
   for (int i = 0; i < argc; i++)
   {
      printf("%s\n", argv[i]);
   }
   if (argc > 1)
   {
      for (int i = 1; i < argc; i++)
      {
         string strArg = argv[i];
      
         if (strArg.find("/PORT") != string::npos)
         {
            portname = strArg.substr(5);
            printf("Use port %s\n", portname.c_str());
         }
         else if (strArg.find("/BAUD") != string::npos)
         {
            string strBaud = strArg.substr(5);
            char *pEnd( NULL );
            nBaudRate = strtol( strBaud.c_str(), &pEnd, 10 );
            nBaudRateMask = GetBaudRate(strBaud);
            printf("Use baud rate %d\n", nBaudRate);
         }
      }
   }

   int32_t fd = SerialPort_Open( portname.c_str(), nBaudRate, 0 );
   if (fd > 0 )
   {
      SerialPort_Write_StringA( fd, "*IDN?\r" );
      uint8_t buf[ 50 ];
      memset( buf, 0, sizeof( buf ) );
      int32_t nRead = SerialPort_Read_ByteArray( fd, buf, sizeof( buf ), 200 );
      if (nRead > 0 )
      {
         printf( "Read returned %s\n", buf);
      }
      SerialPort_Close( fd );
   }

#if( 0 )
   int fd = open (portname.c_str(), O_RDWR | O_NOCTTY | O_SYNC);
   if (fd < 0)
   {
      printf ("error %d opening %s: %s", errno, portname.c_str(), strerror (errno));
      return(-1);
   }
   
   set_interface_attribs (fd, nBaudRateMask, 0);  // set speed to 115,200 bps, 8n1 (no parity)
   set_blocking (fd, 0);                // set no blocking

   write (fd, "*IDN?\r", 7);           // send 7 character greeting

   usleep ((6 + 25) * 100);             // sleep enough to transmit the 7 plus
                                     // receive 25:  approx 100 uS per char transmit
   usleep (50000);   // additional 50 mS wait for other side to process command

   char buf [100];
   memset(buf, 0, sizeof(buf));
   int n = read (fd, buf, sizeof buf);  // read up to 100 characters if ready to read
   if (n > 0)
   {
      printf("Read returned %s\n", buf);
   }
   close(fd);
#endif
   return 0;
}
