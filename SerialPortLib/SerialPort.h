#include <stdint.h>

extern "C"
{
   extern int32_t SerialPort_Open( const char *pszPortName, 
                         uint32_t nBaudRate,
                         uint32_t nParity );

   extern void SerialPort_Close( int32_t fd );

   extern int32_t SerialPort_Write_ByteArray( int32_t fd, 
                                    const uint8_t* cmdBytes, 
                                    uint32_t siz );

   extern int32_t SerialPort_Write_StringA( int32_t fd, 
                                  const char* pszCmd );

   extern int32_t SerialPort_Read_ByteArray( int32_t fd,
                                   uint8_t* respBytes,
                                   uint32_t nMaxBytes, 
                                   uint32_t nTimeoutMS );

}
