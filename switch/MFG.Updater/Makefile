ifeq ($(strip $(DEVKITPRO)),)
$(error "Defina DEVKITPRO. Instale e carregue o ambiente devkitPro antes de compilar.")
endif

TOPDIR ?= $(CURDIR)
include $(DEVKITPRO)/libnx/switch_rules

TARGET      := MFG-Updater
BUILD       := build
SOURCES     := source
INCLUDES    := include
APP_TITLE   := Moonlight Forwarder
APP_AUTHOR  := MFG
APP_VERSION := 0.5.0
ICON        := icon.jpg

ROMFS         := lib/borealis/resources
BOREALIS_PATH := lib/borealis
OUT_SHADERS   := shaders

ARCH     := -march=armv8-a+crc+crypto -mtune=cortex-a57 -mtp=soft -fPIE
CFLAGS   := -g -Wall -Wextra -O2 -ffunction-sections $(ARCH) $(DEFINES)
CFLAGS   += $(INCLUDE) -D__SWITCH__
CXXFLAGS := $(CFLAGS) -std=gnu++20 -Wno-volatile
ASFLAGS  := -g $(ARCH)
LDFLAGS  := -specs=$(DEVKITPRO)/libnx/switch.specs -g $(ARCH) -Wl,-Map,$(notdir $*.map)
LIBS     := -ljson-c -lnx
LIBDIRS  := $(PORTLIBS) $(LIBNX)

include $(TOPDIR)/$(BOREALIS_PATH)/library/borealis.mk

ifneq ($(BUILD),$(notdir $(CURDIR)))
export OUTPUT := $(CURDIR)/$(TARGET)
export TOPDIR := $(CURDIR)
export VPATH := $(foreach dir,$(SOURCES),$(CURDIR)/$(dir))
export DEPSDIR := $(CURDIR)/$(BUILD)

CFILES    := $(foreach dir,$(SOURCES),$(notdir $(wildcard $(dir)/*.c)))
CPPFILES  := $(foreach dir,$(SOURCES),$(notdir $(wildcard $(dir)/*.cpp)))
SFILES    := $(foreach dir,$(SOURCES),$(notdir $(wildcard $(dir)/*.s)))
GLSLFILES := $(foreach dir,$(SOURCES),$(notdir $(wildcard $(dir)/*.glsl)))

export LD := $(CXX)
export OFILES := $(CPPFILES:.cpp=.o) $(CFILES:.c=.o) $(SFILES:.s=.o)

ifneq ($(strip $(ROMFS)),)
ROMFS_SHADERS := $(ROMFS)/$(OUT_SHADERS)
ROMFS_TARGETS := $(patsubst %.glsl,$(ROMFS_SHADERS)/%.dksh,$(GLSLFILES))
export ROMFS_DEPS := $(foreach file,$(ROMFS_TARGETS),$(CURDIR)/$(file))
endif

export INCLUDE := $(foreach dir,$(INCLUDES),-I$(CURDIR)/$(dir)) \
                  $(foreach dir,$(LIBDIRS),-I$(dir)/include) -I$(CURDIR)/$(BUILD)
export LIBPATHS := $(foreach dir,$(LIBDIRS),-L$(dir)/lib)
export APP_ICON := $(TOPDIR)/$(ICON)
export NROFLAGS := --icon="$(APP_ICON)" --nacp="$(CURDIR)/$(TARGET).nacp" --romfsdir="$(CURDIR)/$(ROMFS)"

.PHONY: all clean $(BUILD)
all: $(ROMFS_TARGETS) | $(BUILD)
	@MSYS2_ARG_CONV_EXCL="-D;$(MSYS2_ARG_CONV_EXCL)" $(MAKE) --no-print-directory -C $(BUILD) -f "$(CURDIR)/Makefile"

$(BUILD):
	@mkdir -p $@

$(ROMFS_SHADERS):
	@mkdir -p $@

$(ROMFS_SHADERS)/%_vsh.dksh: %_vsh.glsl | $(ROMFS_SHADERS)
	@echo {vert} $(notdir $<)
	@uam -s vert -o $@ $<

$(ROMFS_SHADERS)/%_tcsh.dksh: %_tcsh.glsl | $(ROMFS_SHADERS)
	@uam -s tess_ctrl -o $@ $<

$(ROMFS_SHADERS)/%_tesh.dksh: %_tesh.glsl | $(ROMFS_SHADERS)
	@uam -s tess_eval -o $@ $<

$(ROMFS_SHADERS)/%_gsh.dksh: %_gsh.glsl | $(ROMFS_SHADERS)
	@uam -s geom -o $@ $<

$(ROMFS_SHADERS)/%_fsh.dksh: %_fsh.glsl | $(ROMFS_SHADERS)
	@echo {frag} $(notdir $<)
	@uam -s frag -o $@ $<

$(ROMFS_SHADERS)/%.dksh: %.glsl | $(ROMFS_SHADERS)
	@uam -s comp -o $@ $<

clean:
	@echo clean ...
	@rm -fr $(BUILD) $(ROMFS_SHADERS) $(TARGET).nro $(TARGET).nacp $(TARGET).elf
else
DEPENDS := $(OFILES:.o=.d)
$(OUTPUT).nro: $(OUTPUT).elf $(OUTPUT).nacp $(ROMFS_DEPS)
$(OUTPUT).elf: $(OFILES)
-include $(DEPENDS)
endif
