#include "USB.h"
#include "USBHIDGamepad.h"

USBHIDGamepad gamepad;

#define COMMAND_BUFFER_SIZE 11
#define BAUD_RATE 115200

#pragma pack(push, 1)
struct DS4Command {
    uint8_t header;
    uint8_t leftX;
    uint8_t leftY;
    uint8_t rightX;
    uint8_t rightY;
    uint8_t l2;
    uint8_t r2;
    uint8_t buttonsLo;
    uint8_t buttonsHi;
    uint8_t checksum;
    uint8_t footer;
};
#pragma pack(pop)

DS4Command cmd = {0, 128, 128, 128, 128, 0, 0, 0, 0, 0, 0};
uint32_t lastBtn = 0;
unsigned long lastSend = 0;

void apply() {
    int8_t lx = cmd.leftX - 128;
    int8_t ly = cmd.leftY - 128;
    int8_t rx = cmd.rightX - 128;
    int8_t ry = cmd.rightY - 128;

    gamepad.leftStick(lx, ly);
    gamepad.rightStick(rx, ry);
    gamepad.leftTrigger(cmd.l2);
    gamepad.rightTrigger(cmd.r2);

    uint32_t btn = (cmd.buttonsHi << 8) | cmd.buttonsLo;
    uint32_t changed = lastBtn ^ btn;

    for (int i = 0; i < 16; i++) {
        if (changed & (1 << i)) {
            if (btn & (1 << i)) {
                gamepad.pressButton(i);
            } else {
                gamepad.releaseButton(i);
            }
        }
    }

    lastBtn = btn;
    lastSend = millis();
}

void setup() {
    Serial.begin(BAUD_RATE);
    delay(500);

    // Просто инициализируем gamepad без кастомизации
    gamepad.begin();
    USB.begin();

    delay(1000);
    Serial.println("[START] USB HID Gamepad ready");
    Serial.println("[INFO] Safe for use in games");
}

void loop() {
    static uint8_t buf[COMMAND_BUFFER_SIZE];
    static uint8_t idx = 0;

    while (Serial.available()) {
        uint8_t b = Serial.read();

        if (idx == 0 && b != 0xFF) {
            continue;
        }

        buf[idx++] = b;

        if (idx == COMMAND_BUFFER_SIZE) {
            if (buf[10] == 0xFE) {
                uint8_t crc = buf[0];
                for (int i = 1; i < 9; i++) {
                    crc ^= buf[i];
                }

                if (crc == buf[9]) {
                    memcpy(&cmd, buf, COMMAND_BUFFER_SIZE);
                    apply();
                    
                    Serial.printf("[Report] LX=%d LY=%d RX=%d RY=%d L2=%d R2=%d BTN=%04X\n",
                                 cmd.leftX, cmd.leftY, cmd.rightX, cmd.rightY,
                                 cmd.l2, cmd.r2, ((cmd.buttonsHi << 8) | cmd.buttonsLo));
                } else {
                    Serial.printf("[ERROR] Checksum: expected %02X, got %02X\n", crc, buf[9]);
                }
            }
            idx = 0;
        }
    }

    if (millis() - lastSend > 20) {
        apply();
        lastSend = millis();
    }

    delay(5);
}
