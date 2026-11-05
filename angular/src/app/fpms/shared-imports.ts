import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { FPMS_PIPES } from '@shared/fpms/pipes';
import { FPMS_UI } from '@shared/fpms/ui';

export const FPMS_IMPORTS = [CommonModule, FormsModule, RouterLink, ...FPMS_PIPES, ...FPMS_UI];
